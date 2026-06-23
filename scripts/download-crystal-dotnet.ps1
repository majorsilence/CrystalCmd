<#
.SYNOPSIS
  Downloads the SAP Crystal Reports .NET runtime installer (for the .NET worker that renders
  reports via the CrystalDecisions assemblies).

.DESCRIPTION
  Unlike the Java jars, the .NET runtime is a system/MSI install (or installed into the Wine
  prefix for the Linux/Wine worker image), not files committed to this repo. SAP does not
  publish a single stable public direct link for it, so provide the installer URL via
  CRYSTALCMD_DOTNET_RUNTIME_URL (copy it from the SAP download portal), or download manually.

  SAP download portal:
    https://origin.softwaredownloads.sap.com/public/site/index.html
  Product: "SAP Crystal Reports, developer version for Microsoft Visual Studio" -> Runtimes
  (CR for VS SP35 runtime, 64-bit and/or 32-bit to match your worker process).

.NOTES
  The Linux/Wine worker images install this runtime as part of the Wine base image build;
  on a Windows workstation install the MSI so the net48 worker can load CrystalDecisions.*.
#>
$ErrorActionPreference = 'Stop'

$url = $env:CRYSTALCMD_DOTNET_RUNTIME_URL
$out = if ($env:CRYSTALCMD_DOTNET_RUNTIME_OUT) { $env:CRYSTALCMD_DOTNET_RUNTIME_OUT } else { Join-Path $env:TEMP 'CRforVS_redist_install_64bit.zip' }

if (-not $url) {
    Write-Host "No CRYSTALCMD_DOTNET_RUNTIME_URL set."
    Write-Host "Download the 'CR for VS' runtime from the SAP portal and either install it or"
    Write-Host "set CRYSTALCMD_DOTNET_RUNTIME_URL to its direct link, then re-run this script:"
    Write-Host "  https://origin.softwaredownloads.sap.com/public/site/index.html"
    exit 0
}

Write-Host "Downloading Crystal Reports .NET runtime to $out ..."
Invoke-WebRequest -Uri $url -OutFile $out
Write-Host "Downloaded $out. Run the installer (or extract into your Wine prefix) to install"
Write-Host "the CrystalDecisions runtime for the .NET worker."
