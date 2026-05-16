#!/usr/bin/env pwsh

$ErrorActionPreference = "Stop"
$CURRENTPATH = $pwd.Path

$obsolete_crystal_url="https://origin-az.softwaredownloads.sap.com/public/file/0020000000665032019"

# download the crystal 13sp20 32 bit runtime, as a zip, and extract it 
# to a folder called "CrystalReports2010Runtime" in the current 
# directory,finding the msi somewhere in the extracted files, and 
# installing it 
# this is required for the obsolete build

$zip_path = Join-Path $CURRENTPATH "CrystalReports2010Runtime.zip"
Invoke-WebRequest -Uri $obsolete_crystal_url -OutFile $zip_path
$extract_path = Join-Path $CURRENTPATH "CrystalReports2010Runtime"
Expand-Archive -Path $zip_path -DestinationPath $extract_path -Force
$msi_path = Get-ChildItem -Path $extract_path -Recurse -Filter "*.msi" | Select-Object -First 1
if (-not $msi_path) { throw "MSI file not found in extracted contents" }
Start-Process -FilePath "msiexec.exe" -ArgumentList "/i `"$($msi_path.FullName)`" /qn" -Wait -NoNewWindow

