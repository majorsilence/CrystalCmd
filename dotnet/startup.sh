#!/usr/bin/env bash

run_x64_service () {
    WINEPREFIX=/majorsilence-wine
    WINEARCH=win64
    sleep 2s
    xvfb-run wine /CrystalCMD/Majorsilence.CrystalCmd.NetFrameworkConsoleServer/Majorsilence.CrystalCmd.NetframeworkConsoleServer.exe &
    sleep 5s
}

run_x64_service

echo "Running $WINEARCH"

while true; do
    responsecode=$(wget --server-response http://127.0.0.1:44355/status 2>&1 | awk '/^  HTTP/{print $2}')
    if [ "$responsecode" != "200" ] ; then
        wget --server-response http://127.0.0.1:44355/status
        break;
    fi
    sleep 1
done
