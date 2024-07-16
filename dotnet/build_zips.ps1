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


$xml = [xml](Get-Content "Majorsilence.CrystalCmd.NetframeworkConsoleServer\Majorsilence.CrystalCmd.NetframeworkConsoleServer.csproj")
$Version=$xml.Project.PropertyGroup.Version

Write-Output "Version: $Version"

Write-Output "Nuget Restore"
dotnet restore

& "$MSBUILD" "Majorsilence.CrystalCmd.NetFrameworkServer.sln" -maxcpucount /verbosity:minimal /property:Configuration="Release" /target:clean /target:rebuild
if ($LastExitCode -ne 0) { throw "Building solution, NetFrameworkServer, failed" }

& "$MSBUILD" "Majorsilence.CrystalCmd.NetFrameworkServer" -maxcpucount /verbosity:minimal /property:Configuration="Release" /target:clean /target:rebuild /p:OutputPath="$CURRENTPATH\build\Majorsilence.CrystalCmd.NetFrameworkServer_temp"
if ($LastExitCode -ne 0) { throw "Publish solution, NetFrameworkServer, failed " }

mkdir  "$CURRENTPATH\build\Majorsilence.CrystalCmd.NetFrameworkServer_$Version"

copy-item "$CURRENTPATH\build\Majorsilence.CrystalCmd.NetFrameworkServer_temp\_PublishedWebsites\Majorsilence.CrystalCmd.NetFrameworkServer\*" "$CURRENTPATH\build\Majorsilence.CrystalCmd.NetFrameworkServer_$Version" -Recurse -Force

Remove-Item -Path "$CURRENTPATH\build\Majorsilence.CrystalCmd.NetFrameworkServer_temp" -Recurse -Force

& "$MSBUILD" "Majorsilence.CrystalCmd.NetframeworkConsoleServer" /p:Configuration=Release /t:Publish /p:PublishProfile=FolderProfile /p:OutputPath="$CURRENTPATH\build\Majorsilence.CrystalCmd.NetFrameworkConsoleServer_$Version"
if ($LastExitCode -ne 0) { throw "Publish solution, NetframeworkConsoleServer, failed" }


Write-Output "Creating zip files"

Compress-Archive -Path "$CURRENTPATH\build\Majorsilence.CrystalCmd.NetFrameworkServer_$Version" -DestinationPath "$CURRENTPATH\build\Majorsilence.CrystalCmd.NetFrameworkServer_$Version.zip"
if ($LastExitCode -ne 0) { throw "Compress-Archive, NetFrameworkServer failed" }

Compress-Archive -Path "$CURRENTPATH\build\Majorsilence.CrystalCmd.NetFrameworkConsoleServer_$Version" -DestinationPath "$CURRENTPATH\build\Majorsilence.CrystalCmd.NetFrameworkConsoleServer_$Version.zip"

Write-Output "Copying nuget packages"
Get-ChildItem -Recurse "$CURRENTPATH\*.nupkg" | Where-Object { $_.FullName -notmatch '\\packages\\' } | Copy-Item -Destination  "$CURRENTPATH/build"

Write-Output "Creating sbom files"
New-Item -ItemType Directory -Force -Path $CURRENTPATH\build\sbom
dotnet CycloneDX "Majorsilence.CrystalCmd.NetFrameworkServer\Majorsilence.CrystalCmd.NetFrameworkServer.csproj" --set-name "Majorsilence.CrystalCmd.NetFrameworkServer" --set-version "$Version" --set-type "Application" --github-username "$env:GITHUB_SBOM_USERNAME" --github-token "$env:GITHUB_SBOM" -o "$CURRENTPATH\build\sbom" --filename "majorsilence-NetFrameworkServer-bom.xml"
if ($LastExitCode -ne 0) { throw "CycloneDX, NetFrameworkServer failed" }

dotnet CycloneDX "Majorsilence.CrystalCmd.NetframeworkConsoleServer\Majorsilence.CrystalCmd.NetframeworkConsoleServer.csproj" --set-name "Majorsilence.CrystalCmd.NetframeworkConsoleServer" --set-version "$Version" --set-type "Application" --github-username "$env:GITHUB_SBOM_USERNAME" --github-token "$env:GITHUB_SBOM" -o "$CURRENTPATH\build\sbom" --filename "majorsilence-NetFrameworkServer-bom.xml"
if ($LastExitCode -ne 0) { throw "CycloneDX, NetFrameworkServer failed" }

cd $CURRENTPATH

if (!(Test-Path -Path ".\packages\NUnit.ConsoleRunner.3.17.0"))
{
	nuget "Install" "NUnit.Console" "-OutputDirectory" "packages" "-Version" "3.17.0"
}

& ".\packages\NUnit.ConsoleRunner.3.17.0\tools\nunit3-console.exe" $CURRENTPATH\Majorsilence.CrystalCmd.NetFrameworkServer\Majorsilence.CrystalCmd.Tests\bin\Release\net48\Majorsilence.CrystalCmd.Tests.dll -result:".\build\test-results.xml"
