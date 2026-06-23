<#
.SYNOPSIS
  Reconstructs java/CrystalCmd/lib/ (Windows). The SAP Crystal Reports runtime jars are
  proprietary and are NOT committed to source control; this fetches them plus the app's
  own dependencies.

.DESCRIPTION
  Sources:
    - SAP "Crystal Reports for Eclipse SP32 Runtime Libraries" zip -> Crystal jars + SAP-bundled OSS
    - Maven Central                                                -> this app's own dependencies

  Environment overrides: CRYSTALCMD_CR4E_URL, CRYSTALCMD_CR4E_ZIP, CRYSTALCMD_LIB_DIR, MAVEN_BASE_URL

  SAP download portal (Java and .NET runtimes):
    https://origin.softwaredownloads.sap.com/public/site/index.html
#>
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$crUrl  = if ($env:CRYSTALCMD_CR4E_URL) { $env:CRYSTALCMD_CR4E_URL } else { 'https://origin-az.softwaredownloads.sap.com/public/file/0020000000018922026' }
$libDir = if ($env:CRYSTALCMD_LIB_DIR)  { $env:CRYSTALCMD_LIB_DIR }  else { Join-Path $repoRoot 'java/CrystalCmd/lib' }
$maven  = if ($env:MAVEN_BASE_URL)      { $env:MAVEN_BASE_URL }      else { 'https://repo1.maven.org/maven2' }

New-Item -ItemType Directory -Force -Path $libDir | Out-Null
$tmp = Join-Path $env:TEMP ("cr4e_" + [guid]::NewGuid().ToString())
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
try {
    if ($env:CRYSTALCMD_CR4E_ZIP -and (Test-Path $env:CRYSTALCMD_CR4E_ZIP)) {
        Write-Host "Using cached SP32 zip: $($env:CRYSTALCMD_CR4E_ZIP)"
        $zip = $env:CRYSTALCMD_CR4E_ZIP
    } else {
        Write-Host 'Downloading Crystal Reports for Eclipse SP32 runtime...'
        $zip = Join-Path $tmp 'cr4e.zip'
        Invoke-WebRequest -Uri $crUrl -OutFile $zip
    }

    Write-Host "Extracting Crystal runtime jars into $libDir ..."
    $extract = Join-Path $tmp 'x'
    Expand-Archive -Path $zip -DestinationPath $extract -Force
    Get-ChildItem -Path (Join-Path $extract 'lib') -Filter '*.jar' | Copy-Item -Destination $libDir -Force

    function Fetch($url, $out) {
        Write-Host "  $out"
        try { Invoke-WebRequest -Uri $url -OutFile (Join-Path $libDir $out) }
        catch { Write-Warning "failed to download $out from $url" }
    }

    Write-Host 'Downloading application dependencies from Maven Central ...'
    Fetch "$maven/com/google/code/gson/gson/2.8.6/gson-2.8.6.jar" 'gson-2.8.6.jar'
    Fetch "$maven/com/h2database/h2/1.4.196/h2-1.4.196.jar" 'h2-1.4.196.jar'
    Fetch "$maven/net/sourceforge/csvjdbc/csvjdbc/1.0.37/csvjdbc-1.0.37.jar" 'csvjdbc-1.0.37.jar'
    Fetch "$maven/commons-fileupload/commons-fileupload/1.3.3/commons-fileupload-1.3.3.jar" 'commons-fileupload-1.3.3.jar'
    Fetch "$maven/commons-io/commons-io/2.6/commons-io-2.6.jar" 'commons-io-2.6.jar'

    $count = (Get-ChildItem -Path $libDir -Filter '*.jar').Count
    Write-Host ""
    Write-Host "Done: $count jars in $libDir"
    Write-Host "Note: SP32 is newer than the original vendored set (some bundled OSS jars differ"
    Write-Host "in version/name); build against the whole directory rather than fixed jar names."
} finally {
    Remove-Item -Recurse -Force $tmp
}
