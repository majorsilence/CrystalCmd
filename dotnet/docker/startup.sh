#!/usr/bin/env bash

run_x64_service () {
    WINEPREFIX=/majorsilence-wine
    WINEARCH=win64
    sleep 2s

    mkdir -p /majorsilence-wine/drive_c/users/root/AppData/Local/Temp/majorsilence/crystalcmd
    # start the CrystalCMD console service within wine and xvfb
    if [ -f "/CrystalCMD/Majorsilence.CrystalCmd.NetFrameworkConsole/x64/Majorsilence.CrystalCmd.NetframeworkConsole.exe" ]; then
        xvfb-run wine /CrystalCMD/Majorsilence.CrystalCmd.NetFrameworkConsole/x64/Majorsilence.CrystalCmd.NetframeworkConsole.exe &
    else
        xvfb-run wine /CrystalCMD/Majorsilence.CrystalCmd.NetFrameworkConsole/Majorsilence.CrystalCmd.NetframeworkConsole.exe &
    fi

    # start the CrystalCMD asp.net core server
    dotnet /CrystalCMD/Majorsilence.CrystalCmd.Server/Majorsilence.CrystalCmd.Server.dll &

    sleep 15s
}

run_x64_service

echo "Running $WINEARCH"

while true; do

    # check if the web service is healthy

    responsecode=$(wget --tries=1 --timeout=5 --server-response -O /dev/null http://127.0.0.1:5000/healthz/ready 2>&1 | awk '/^  HTTP/{print $2}')
    if [ "$responsecode" != "200" ] ; then
        echo "CrystalCMD http service Health check failed with response code $responsecode"
        wget --tries=1 --timeout=5 --server-response http://127.0.0.1:5000/healthz/ready
        exit 1
    fi

    # chck if the wine process is still running
    pcount=$(pgrep -fc Majorsilence.CrystalCmd.NetframeworkConsole.exe)
    if [ "$pcount" -lt 1 ] ; then
        echo "CrystalCMD NetframeworkConsole process not running"
        exit 1
    fi

    # wait before rechecking
    sleep 1
done
