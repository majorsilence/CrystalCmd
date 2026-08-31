#!/usr/bin/env bash
#
# CrystalCMD worker entrypoint (ubuntu and alpine worker images).
#
# Runs the .NET Framework worker under Wine, which needs an X server, and restarts
# it when Wine crashes. Four properties matter, and each one was previously broken:
#
#   1. Exactly one Xvfb exists at a time. XVFB_PID was read but never assigned, so
#      the cleanup was dead code and every restart leaked an X server. A crash
#      loop then grew until the container hit its memory ceiling.
#   2. The restart counter survives across shells. It was incremented inside a
#      `tail | while read` subshell, so the main loop never saw it: the give-up
#      threshold never fired and the loop restarted Wine indefinitely.
#   3. The container exits when it gives up. Nothing else makes an orchestrator
#      recreate it -- an OOM kill lands on the Wine process, not on this shell.
#   4. Exactly one watchdog exists. monitor_wine_log was backgrounded again on
#      every restart, so monitors (and their `tail -F` processes) accumulated too.

set -uo pipefail

WINE_LOG=/majorsilence-wine/wine_crystalcmd.log
STATE_DIR=/majorsilence-wine/state
TSFILE=/tmp/wine_err_timestamps.txt
KILLCOUNT_FILE="$STATE_DIR/killcount"
HEARTBEAT_FILE="$STATE_DIR/heartbeat"

# Treat this many matching error lines within WINDOW seconds as a crash.
THRESHOLD=1
WINDOW=160

# Give up after this many restarts and exit non-zero so the orchestrator can
# recreate the container. Restarting in place forever hides a broken worker
# behind a container that still looks alive.
MAX_RESTARTS=10

# The Wine log is appended to across restarts and tailed by the watchdog. Left
# unbounded it fills the container's writable layer. tail -F follows by name and
# reopens after the rotation below.
MAX_LOG_BYTES=$((50 * 1024 * 1024))

WINE_PID=""
XVFB_PID=""
MONITOR_PID=""
DISPLAY_NUM=99

mkdir -p "$STATE_DIR" "$(dirname "$WINE_LOG")"
touch "$WINE_LOG"
echo 0 > "$KILLCOUNT_FILE"
: > "$TSFILE"

# Restart bookkeeping goes through a file rather than a shell variable: the
# watchdog runs in a background subshell and cannot write to the main shell's
# memory. That was the original defect and a file is the least surprising fix.
read_killcount() {
    local value
    value="$(cat "$KILLCOUNT_FILE" 2>/dev/null || echo 0)"
    case "$value" in
        ''|*[!0-9]*) echo 0 ;;
        *)           echo "$value" ;;
    esac
}

bump_killcount() {
    echo $(( $(read_killcount) + 1 )) > "$KILLCOUNT_FILE"
}

log() {
    echo "$(date -u '+%Y-%m-%dT%H:%M:%SZ') $*" | tee -a "$WINE_LOG"
}

rotate_log_if_needed() {
    local size
    size=$(wc -c < "$WINE_LOG" 2>/dev/null || echo 0)
    if [ "$size" -gt "$MAX_LOG_BYTES" ]; then
        mv -f "$WINE_LOG" "${WINE_LOG}.1" 2>/dev/null || true
        : > "$WINE_LOG"
        echo "$(date -u '+%Y-%m-%dT%H:%M:%SZ') [rotate] wine log rotated at ${size} bytes" >> "$WINE_LOG"
    fi
}

stop_xvfb() {
    # Kill the server directly by PID. The previous version used `kill -- -$PID`
    # to signal a process group -- which would have hit this script's own group,
    # and was unreachable anyway because XVFB_PID was never set. It then fell back
    # to `pkill -9 Xvfb`, which kills every X server on the host indiscriminately.
    if [ -n "$XVFB_PID" ] && kill -0 "$XVFB_PID" 2>/dev/null; then
        echo "Terminating Xvfb (pid $XVFB_PID)"
        kill "$XVFB_PID" 2>/dev/null || true
        for _ in 1 2 3 4 5; do
            kill -0 "$XVFB_PID" 2>/dev/null || break
            sleep 1
        done
        kill -9 "$XVFB_PID" 2>/dev/null || true
    fi
    XVFB_PID=""

    # Belt and braces, scoped to our own display: a leaked server keeps its lock
    # file, and the next start would pick another display and strand this one.
    pkill -f "Xvfb :${DISPLAY_NUM}([^0-9]|$)" 2>/dev/null || true
    rm -f "/tmp/.X${DISPLAY_NUM}-lock" "/tmp/.X11-unix/X${DISPLAY_NUM}" 2>/dev/null || true
}

kill_all_wine_processes() {
    echo "Attempting graceful Wine shutdown in ${WINEPREFIX}..."
    wineserver -k 2>/dev/null || true
    sleep 5

    if pgrep -f "wineserver.*${WINEPREFIX}" >/dev/null 2>&1; then
        echo "Wine processes still running, forcing shutdown with -k9..."
        wineserver -k9 2>/dev/null || true
        sleep 2
    fi

    pkill -9 -f "Majorsilence.CrystalCmd.NetframeworkConsole.exe" 2>/dev/null || true
    stop_xvfb
    echo "Cleanup complete."
}

start_xvfb() {
    stop_xvfb

    # Started explicitly rather than through xvfb-run, which gives the caller no
    # way to learn the server's PID -- so it could never be stopped. Plain
    # xvfb-run also defaults to display :99 and fails outright if one is already
    # there, which is exactly what a leaked server guarantees.
    Xvfb ":${DISPLAY_NUM}" -screen 0 1024x768x24 -nolisten tcp >>"$WINE_LOG" 2>&1 &
    XVFB_PID=$!
    export DISPLAY=":${DISPLAY_NUM}"

    for _ in 1 2 3 4 5 6 7 8 9 10; do
        if ! kill -0 "$XVFB_PID" 2>/dev/null; then
            log "Xvfb failed to start"
            XVFB_PID=""
            return 1
        fi
        if [ -e "/tmp/.X${DISPLAY_NUM}-lock" ]; then
            log "Xvfb ready on ${DISPLAY} (pid $XVFB_PID)"
            return 0
        fi
        sleep 1
    done

    log "Xvfb did not become ready on ${DISPLAY}"
    return 1
}

monitor_wine_log() {
    # Watch the Wine log for crash indicators and trigger a cleanup once THRESHOLD
    # of them land inside WINDOW seconds.
    #
    # Reads via process substitution rather than a pipe so the loop body runs in
    # this shell. The counter still goes to a file, because this whole function is
    # itself backgrounded. It no longer truncates WINE_LOG on entry -- that raced
    # with the `tee` writing to it and destroyed the history of every restart.
    #
    # NOTE: "wait timed out" is a generic Wine message that can also appear during
    # a legitimately slow render. If large reports are seen dying mid-export, this
    # pattern is the first thing to suspect.
    local pattern='wait timed out|RpcAssoc_BindConnection rejected bind|IRemUnknown_RemRelease failed|The process was terminated due to an unhandled exception'

    while read -r line; do
        if echo "$line" | grep -Eqi "$pattern"; then
            ts=$(date +%s)
            echo "$ts" >> "$TSFILE"
            awk -v now="$ts" -v w="$WINDOW" '$1 >= now-w' "$TSFILE" > "${TSFILE}.new" 2>/dev/null || true
            mv "${TSFILE}.new" "$TSFILE" 2>/dev/null || true
            cnt=$(wc -l < "$TSFILE" 2>/dev/null || echo 0)

            if [ "$cnt" -ge "$THRESHOLD" ]; then
                log "Detected $cnt Wine errors within last $WINDOW seconds; killing Wine (pid: $WINE_PID)"
                kill_all_wine_processes
                bump_killcount
                : > "$TSFILE"
                log "[watchdog] restarts=$(read_killcount); exiting monitor"
                return 1
            fi
        fi
    done < <(tail -n0 -F "$WINE_LOG" 2>/dev/null)
}

start_monitor_if_needed() {
    # Guarded. The old code backgrounded a fresh monitor on every restart, so
    # monitors and their tail -F processes accumulated alongside the X servers --
    # which is why cleanup resorted to `pkill -9 tail`, killing every tail on the
    # host including the ones it still needed.
    if [ -z "$MONITOR_PID" ] || ! kill -0 "$MONITOR_PID" 2>/dev/null; then
        monitor_wine_log &
        MONITOR_PID=$!
        log "(re)started monitor (pid: $MONITOR_PID)"
    fi
}

is_worker_running() {
    pgrep -f Majorsilence.CrystalCmd.NetframeworkConsole.exe >/dev/null 2>&1
}

run_x64_wine_worker_service() {
    sleep 2s

    mkdir -p /majorsilence-wine/drive_c/users/root/AppData/Local/Temp/majorsilence/crystalcmd

    local cmd_path
    if [ -f "/CrystalCMD/Majorsilence.CrystalCmd.NetframeworkConsole/x64/Majorsilence.CrystalCmd.NetframeworkConsole.exe" ]; then
        cmd_path="/CrystalCMD/Majorsilence.CrystalCmd.NetframeworkConsole/x64/Majorsilence.CrystalCmd.NetframeworkConsole.exe"
    else
        cmd_path="/CrystalCMD/Majorsilence.CrystalCmd.NetframeworkConsole/Majorsilence.CrystalCmd.NetframeworkConsole.exe"
    fi

    if ! start_xvfb; then
        log "Not starting the worker without an X server"
        return 1
    fi

    log "Starting CrystalCMD: $cmd_path on $DISPLAY"
    # Appends. The old `tee "$WINE_LOG"` truncated on every restart and raced with
    # the watchdog doing the same thing.
    wine "$cmd_path" >>"$WINE_LOG" 2>&1 &

    local waittime=0
    local maxwait=25
    while ! is_worker_running && [ "$waittime" -lt "$maxwait" ]; do
        echo "Waiting for CrystalCMD worker to start... ($waittime/$maxwait)"
        sleep 5
        waittime=$((waittime+5))
    done

    WINE_PID=$(pgrep -f Majorsilence.CrystalCmd.NetframeworkConsole.exe | head -n1 || true)
    if [ -z "$WINE_PID" ]; then
        log "Failed to find Majorsilence.CrystalCmd.NetframeworkConsole.exe pid after start"
    else
        log "Detected worker pid: $WINE_PID"
    fi

    start_monitor_if_needed
}

# Leave nothing behind on the way out. Without this a SIGTERM orphans both Wine
# and the X server.
on_exit() {
    log "Shutting down"
    kill_all_wine_processes
    if [ -n "$MONITOR_PID" ]; then
        kill "$MONITOR_PID" 2>/dev/null || true
    fi
}
trap on_exit EXIT INT TERM

log "Starting CrystalCMD worker"
run_x64_wine_worker_service
start_monitor_if_needed

log "Entering main monitoring loop"

while true; do
    rotate_log_if_needed

    if is_worker_running; then
        # A liveness/healthcheck can watch this file's mtime; nothing else in the
        # container distinguishes rendering reports from sitting in a restart loop.
        date -u '+%Y-%m-%dT%H:%M:%SZ' > "$HEARTBEAT_FILE"
    else
        killcount="$(read_killcount)"
        log "CrystalCMD NetframeworkConsole process not running (restarts=$killcount)"

        # Counts every restart, not only watchdog kills. The worker exiting for any
        # other reason used to restart forever without incrementing anything.
        if [ "$killcount" -ge "$MAX_RESTARTS" ]; then
            log "Worker has been restarted $killcount times; exiting so the container is recreated"
            exit 1
        fi

        kill_all_wine_processes

        # There was no backoff at all: a worker that failed immediately was
        # restarted immediately, forever.
        backoff=$((2 ** killcount))
        if [ "$backoff" -gt 60 ]; then
            backoff=60
        fi
        log "Waiting $backoff seconds before restart (restarts=$killcount)"
        sleep "$backoff"

        bump_killcount
        run_x64_wine_worker_service
    fi

    sleep 1
done
