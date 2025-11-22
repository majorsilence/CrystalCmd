#!/usr/bin/env pwsh

$ErrorActionPreference = "Stop"
$CURRENTPATH = $pwd.Path
$MSBUILD = "C:\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
If (Test-Path "C:\BuildTools\MSBuild\Current\Bin\MSBuild.exe") {
	$MSBUILD = "C:\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
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

function Trim-Whitespace {
    param (
        [string]$InputString
    )

    return $InputString.Trim()
}

function clean_bin_obj() {
	Set-Location "$CURRENTPATH"
	# DELETE ALL "BIN" and "OBJ" FOLDERS
	get-childitem -Include bin,obj -Recurse -force | Remove-Item -Force -Recurse
}

function clean_build() {
	# This function is only for when building locally outside of docker
	Write-Host "Begin clean" -ForegroundColor Green
	if (!(Test-Path "build")) {
		mkdir build
	}
	Set-Location build
	Remove-Item * -Recurse -Force
	Set-Location ..

	clean_bin_obj
}

clean_build
cd "$CURRENTPATH\Majorsilence.CrystalCmd.NetFrameworkServer"


$xml = [xml](Get-Content "Directory.Build.props")
$Version = Trim-Whitespace -InputString $xml.Project.PropertyGroup.Version

Write-Output "Version: $Version"

Write-Output "Nuget Restore"
dotnet restore

& "$MSBUILD" "Majorsilence.CrystalCmd.NetFrameworkServer.sln" -maxcpucount /verbosity:minimal /property:Configuration="Release" /target:clean /target:rebuild
if ($LastExitCode -ne 0) { throw "Building solution, NetFrameworkServer, failed" }

& "$MSBUILD" "Majorsilence.CrystalCmd.Console\Majorsilence.CrystalCmd.NetframeworkConsole.csproj" /p:Configuration=Release /t:Publish /p:PublishProfile=FolderProfile /p:OutputPath="$CURRENTPATH\build\Majorsilence.CrystalCmd.NetframeworkConsole$Version"
if ($LastExitCode -ne 0) { throw "Publish solution, NetframeworkConsole, failed" }

dotnet publish "Majorsilence.CrystalCmd.Server" --configuration Release --output "$CURRENTPATH\build\Majorsilence.CrystalCmd.Server-win-x64-$Version" --runtime win-x64 --framework net8.0-windows

dotnet publish "Majorsilence.CrystalCmd.Server" --configuration Release --output "$CURRENTPATH\build\Majorsilence.CrystalCmd.Server$Version" --framework net8.0

Write-Output "Creating zip files"

Compress-Archive -Path "$CURRENTPATH\build\Majorsilence.CrystalCmd.NetframeworkConsole$Version" -DestinationPath "$CURRENTPATH\build\Majorsilence.CrystalCmd.NetframeworkConsole$Version.zip"

Compress-Archive -Path "$CURRENTPATH\build\Majorsilence.CrystalCmd.Server-win-x64-$Version\*" -DestinationPath "$CURRENTPATH\build\Majorsilence.CrystalCmd.Server-win-x64-$Version.zip"

Compress-Archive -Path "$CURRENTPATH\build\Majorsilence.CrystalCmd.Server$Version\*" -DestinationPath "$CURRENTPATH\build\Majorsilence.CrystalCmd.Server$Version.zip"

Write-Output "Copying nuget packages"
Get-ChildItem -Recurse "$CURRENTPATH\*.nupkg" | Where-Object { $_.FullName -notmatch '\\packages\\' } | Copy-Item -Destination  "$CURRENTPATH/build"

Write-Output "Creating sbom files"
New-Item -ItemType Directory -Force -Path $CURRENTPATH\build\sbom
if (!(Get-Command -Name CycloneDX -ErrorAction SilentlyContinue)) {
	dotnet tool install --global CycloneDX --ignore-failed-sources
}

dotnet CycloneDX "Majorsilence.CrystalCmd.Console\Majorsilence.CrystalCmd.NetframeworkConsole.csproj" --set-name "Majorsilence.CrystalCmd.NetframeworkConsole" --set-version "$Version" --set-type "Application" --github-username "$env:GITHUB_SBOM_USERNAME" --github-token "$env:GITHUB_SBOM" -o "$CURRENTPATH\build\sbom" --filename "majorsilence-NetFrameworkConsole-bom.xml"
if ($LastExitCode -ne 0) { throw "CycloneDX, NetFrameworkConsole failed" }

dotnet CycloneDX "Majorsilence.CrystalCmd.Server\Majorsilence.CrystalCmd.Server.csproj" --set-name "Majorsilence.CrystalCmd.Server" --set-version "$Version" --set-type "Application" --github-username "$env:GITHUB_SBOM_USERNAME" --github-token "$env:GITHUB_SBOM" -o "$CURRENTPATH\build\sbom" --filename "majorsilence-Server-bom.xml"
if ($LastExitCode -ne 0) { throw "CycloneDX, Server failed" }

cd $CURRENTPATH

New-Item -ItemType Directory -Path ".\build\TestResults" -Force | Out-Null
dotnet test .\Majorsilence.CrystalCmd.NetFrameworkServer\Majorsilence.CrystalCmd.NetFrameworkServer.sln --configuration Release --logger "nunit"
if ($LastExitCode -ne 0) { throw "Unit tests failed" }
