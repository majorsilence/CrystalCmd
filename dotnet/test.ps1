#!/usr/bin/env pwsh

$ErrorActionPreference = "Stop"
$CURRENTPATH = $pwd.Path

# Build everything first, then test with --no-build. `dotnet test` on the solution
# otherwise builds each test project on its own MSBuild node while other projects'
# tests are already running, and the end-to-end client tests launch the worker from
# the Console project's bin folder: a node rebuilding that project then cannot
# overwrite the DLLs the running worker has loaded ("The file is locked by:
# Majorsilence.CrystalCmd.NetframeworkConsole"), and the step fails on a race.
dotnet build .\Majorsilence.CrystalCmd.NetFrameworkServer\Majorsilence.CrystalCmd.NetFrameworkServer.sln --configuration Release
if ($LastExitCode -ne 0) { throw "Build failed" }

dotnet test .\Majorsilence.CrystalCmd.NetFrameworkServer\Majorsilence.CrystalCmd.NetFrameworkServer.sln --configuration Release --no-build --logger "nunit" -p:TestTfmsInParallel=false
if ($LastExitCode -ne 0) { throw "Unit tests failed" }
