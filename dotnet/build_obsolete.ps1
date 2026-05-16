#!/usr/bin/env pwsh

$ErrorActionPreference = "Stop"
$CURRENTPATH = $pwd.Path
$MSBUILD = "C:\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
If (Test-Path "C:\BuildTools\MSBuild\Current\Bin\MSBuild.exe") {
	$MSBUILD = "C:\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
}
ElseIf (Test-Path "C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe") {
	$MSBUILD = "C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe"
}
ElseIf (Test-Path "C:\BuildTools\MSBuild\17.0\Bin\MSBuild.exe") {
	$MSBUILD = "C:\BuildTools\MSBuild\17.0\Bin\MSBuild.exe"
}
ElseIf (Test-Path "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe") {
	$MSBUILD = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
}
else {
	$MSBUILD="msbuild"
}


cd "$CURRENTPATH\Majorsilence.CrystalCmd.NetFrameworkServer"


& "$MSBUILD" "Majorsilence.CrystalCmd.Server.Common\Majorsilence.CrystalCmd.Server.Common.csproj" -maxcpucount /verbosity:minimal /property:Configuration="Release" /target:clean /target:rebuild /p:UseOldCrystalReportsReferences="yes"
if ($LastExitCode -ne 0) { throw "Building solution, NetFrameworkServer, failed" }


Write-Output "Copying nuget packages"
Get-ChildItem -Recurse "$CURRENTPATH\*Obsolete*.nupkg" | Where-Object { $_.FullName -notmatch '\\packages\\' } | Copy-Item -Destination  "$CURRENTPATH/build"


cd $CURRENTPATH
