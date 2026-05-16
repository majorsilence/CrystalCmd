#!/usr/bin/env pwsh

$ErrorActionPreference = "Stop"
$CURRENTPATH = $pwd.Path

dotnet test .\Majorsilence.CrystalCmd.NetFrameworkServer\Majorsilence.CrystalCmd.NetFrameworkServer.sln --configuration Release --logger "nunit" -p:TestTfmsInParallel=false
if ($LastExitCode -ne 0) { throw "Unit tests failed" }
