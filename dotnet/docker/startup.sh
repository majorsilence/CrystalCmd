#!/usr/bin/env bash


WINE_LOG=/majorsilence-wine/wine_crystalcmd.log
TSFILE=/tmp/wine_err_timestamps.txt

WINE_PID=""

killcount=0


kill_all_wine_processes() {
    echo "Attempting graceful Wine shutdown in $WINEPREFIX..."
    # Send a SIGINT to all Wine processes in the prefix
    wineserver -k
    sleep 5
    
    # Check if any wine processes are still running and force kill if necessary
    if pgrep -f "wineserver.*$(echo $WINEPREFIX | sed 's/\//\\\//g')" > /dev/null; then
        echo "Wine processes still running, forcing shutdown with -k9..."
        wineserver -k9
        sleep 2
    fi

    # Kill the Xvfb process group
    if [ ! -z "$XVFB_PID" ]; then
        echo "Terminating Xvfb process group: -$XVFB_PID"
        # Kill the entire process group with a negative PID
        kill -- "-$XVFB_PID" 2>/dev/null
    fi

    
    echo "Cleanup complete."
}

in_kill_state=0

monitor_wine_log() {
    THRESHOLD=1   # number of matching error lines to treat as failure
    WINDOW=160     # seconds window for threshold
    TSFILE="$TSFILE"
    : > "$WINE_LOG"
    : > "$TSFILE"

    tail -n0 -F "$WINE_LOG" 2>/dev/null | while read -r line; do
        echo "$line" | grep -Ei "wait timed out|RpcAssoc_BindConnection rejected bind|IRemUnknown_RemRelease failed|The process was terminated due to an unhandled exception" >/dev/null 2>&1
        if [ $? -eq 0 ]; then
            ts=$(date +%s)
            echo "$ts" >> "$TSFILE"
            # prune timestamps older than WINDOW
            awk -v now="$ts" -v w="$WINDOW" '$1 >= now-w' "$TSFILE" > "${TSFILE}.new" 2>/dev/null || true
            mv "${TSFILE}.new" "$TSFILE" 2>/dev/null || true
            cnt=$(wc -l < "$TSFILE" 2>/dev/null || echo 0)
            if [ "$cnt" -ge "$THRESHOLD" ]; then
                echo "Detected $cnt Wine errors within last $WINDOW seconds; killing Wine (pid: $WINE_PID)" | tee -a "$WINE_LOG"

                in_kill_state=1
                kill_all_wine_processes
                killcount=$((killcount+1))
                killall -9 tail
                killall -9 Xvfb
                # also attempt to kill any wine processes running the exe
                pgrep -f Majorsilence.CrystalCmd.NetframeworkConsole.exe | xargs -r kill 2>/dev/null || true
                echo "Wine process has been killed $killcount times; exiting monitor" | tee -a "$WINE_LOG"
                in_kill_state=0
                exit 1      
            fi
        fi
    done
}


is_worker_running() {
    pcount=$(pgrep -fc Majorsilence.CrystalCmd.NetframeworkConsole.exe)
    if [ "$pcount" -ge 1 ] ; then
        return 0
    else
        return 1
    fi
}

run_x64_wine_worker_service () {
    WINEPREFIX=/majorsilence-wine
    WINEARCH=win64
    sleep 2s

    mkdir -p /majorsilence-wine/drive_c/users/root/AppData/Local/Temp/majorsilence/crystalcmd
    # prepare wine log and watchdog
   
    # start the CrystalCMD console service within wine and xvfb, redirect output to a logfile and console
    if [ -f "/CrystalCMD/Majorsilence.CrystalCmd.NetframeworkConsole/x64/Majorsilence.CrystalCmd.NetframeworkConsole.exe" ]; then
        xvfb-run wine /CrystalCMD/Majorsilence.CrystalCmd.NetframeworkConsole/x64/Majorsilence.CrystalCmd.NetframeworkConsole.exe 2>&1 | tee "$WINE_LOG" &
    else
        xvfb-run wine /CrystalCMD/Majorsilence.CrystalCmd.NetframeworkConsole/Majorsilence.CrystalCmd.NetframeworkConsole.exe 2>&1 | tee "$WINE_LOG" &
    fi  

    # if not running, wait a bit and try again, wait up to 25 seconds
    local waittime=0
    local maxwait=25
    local is_running=1
    while [ "$is_running" -ne 0 ] && [ "$waittime" -lt "$maxwait" ] ; do
        echo "Waiting for CrystalCMD worker to start... ($waittime/$maxwait)"
        sleep 10
        waittime=$((waittime+10))
        is_worker_running
        is_running=$?
    done
   
    # find the wine process id (may be the wine wrapper); prefer the actual exe process
    WINE_PID=$(pgrep -f Majorsilence.CrystalCmd.NetframeworkConsole.exe | head -n1 || true)

    monitor_wine_log &
}


echo "Starting CrystalCMD worker"
run_x64_wine_worker_service

echo "Entering main monitoring loop"

while true; do
    # is_worker_running returns 0 when the worker is running.
    is_worker_running
    is_running=$?

    # if exit code is non-zero, the worker is NOT running
    if [ "$is_running" -ne 0 ] ; then
        echo "CrystalCMD NetframeworkConsole process not running"

        if [ "$killcount" -ge 10 ] ; then
            echo "Wine process has been killed $killcount times; exiting main loop" | tee -a "$WINE_LOG"
            exit 1
        else
            echo "Restarting Wine CrystalCMD worker service timestamp: $(date)" | tee -a "$WINE_LOG"
            if [ $in_kill_state -eq 1 ] ; then
                echo "Currently in kill state; waiting 10 seconds before restart"
                sleep 2
            else
                run_x64_wine_worker_service
            fi
        fi
    fi
    # wait before rechecking

    sleep 1
done