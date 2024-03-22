#!/usr/bin/env bash

run_x86_service () {
    WINEPREFIX=/majorsilence-wine-x86
    WINEARCH=win32
    sleep 2s
    xvfb-run wine /CrystalCMD/Majorsilence.CrystalCmd.NetframeworkConsoleServer/x86/Release/net48/Majorsilence.CrystalCmd.NetframeworkConsoleServer.exe &
    sleep 2s
}

run_x64_service () {
    WINEPREFIX=/majorsilence-wine
    WINEARCH=win64
    sleep 2s
    xvfb-run wine /CrystalCMD/Majorsilence.CrystalCmd.NetframeworkConsoleServer/x64/Release/net48/Majorsilence.CrystalCmd.NetframeworkConsoleServer.exe &
    sleep 2s
}

if [[ -z "${OVERRIDE_WINEARCH_AS_X64}" ]]; then
    echo "OVERRIDE_WINEARCH_AS_X64 not set.  Defaulting to win32"
    run_x86_service
else
    run_x64_service
fi

echo "Running $WINEARCH"

while true; do
    responsecode=$(wget --server-response http://127.0.0.1:44355/status 2>&1 | awk '/^  HTTP/{print $2}')
    if [ "$responsecode" != "200" ] ; then
        wget --server-response http://127.0.0.1:44355/status
        break;
    fi
    sleep 1
done
