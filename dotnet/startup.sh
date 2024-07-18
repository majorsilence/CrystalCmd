#!/usr/bin/env bash

run_x64_service () {
    WINEPREFIX=/majorsilence-wine
    WINEARCH=win64
    sleep 2s
    if [ -f "/CrystalCMD/Majorsilence.CrystalCmd.NetFrameworkConsoleServer/x64/Majorsilence.CrystalCmd.NetframeworkConsoleServer.exe" ]; then
        xvfb-run wine /CrystalCMD/Majorsilence.CrystalCmd.NetFrameworkConsoleServer/x64/Majorsilence.CrystalCmd.NetframeworkConsoleServer.exe &
    else
        xvfb-run wine /CrystalCMD/Majorsilence.CrystalCmd.NetFrameworkConsoleServer/Majorsilence.CrystalCmd.NetframeworkConsoleServer.exe &
    fi
    
    sleep 5s
}

run_x64_service

echo "Running $WINEARCH"

while true; do
    responsecode=$(wget --tries=1 --timeout=5 --server-response http://127.0.0.1:44355/healthz/ready 2>&1 | awk '/^  HTTP/{print $2}')
    if [ "$responsecode" != "200" ] ; then
        echo "Health check failed with response code $responsecode"
        wget --tries=1 --timeout=5 --server-response http://127.0.0.1:44355/healthz/ready
        exit 1
    fi
    sleep 1
done
