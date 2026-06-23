#!/usr/bin/env bash
#
# Reconstructs java/CrystalCmd/lib/ which is intentionally NOT committed (the SAP Crystal
# Reports runtime jars are proprietary and must not be redistributed in source control).
#
# Sources:
#   - SAP "Crystal Reports for Eclipse SP32 Runtime Libraries" zip  -> Crystal jars + SAP-bundled OSS
#   - Maven Central                                                 -> this app's own dependencies
#
# Usage:
#   scripts/download-crystal-libs.sh
#
# Environment overrides:
#   CRYSTALCMD_CR4E_URL   alternate SP zip URL (default: the SAP SP32 public link)
#   CRYSTALCMD_CR4E_ZIP   use an already-downloaded zip instead of fetching
#   CRYSTALCMD_LIB_DIR    target lib directory (default: <repo>/java/CrystalCmd/lib)
#   MAVEN_BASE_URL        Maven mirror (default: https://repo1.maven.org/maven2)
#
# SAP download portal (for manual/alternate downloads, both Java and .NET runtimes):
#   https://origin.softwaredownloads.sap.com/public/site/index.html
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

CR_ZIP_URL="${CRYSTALCMD_CR4E_URL:-https://origin-az.softwaredownloads.sap.com/public/file/0020000000018922026}"
LIB_DIR="${CRYSTALCMD_LIB_DIR:-$REPO_ROOT/java/CrystalCmd/lib}"
MAVEN="${MAVEN_BASE_URL:-https://repo1.maven.org/maven2}"

mkdir -p "$LIB_DIR"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

if [ -n "${CRYSTALCMD_CR4E_ZIP:-}" ] && [ -f "${CRYSTALCMD_CR4E_ZIP}" ]; then
    echo "Using cached SP32 zip: $CRYSTALCMD_CR4E_ZIP"
    zip="$CRYSTALCMD_CR4E_ZIP"
else
    echo "Downloading Crystal Reports for Eclipse SP32 runtime..."
    curl -fL --retry 3 -o "$tmp/cr4e.zip" "$CR_ZIP_URL"
    zip="$tmp/cr4e.zip"
fi

echo "Extracting Crystal runtime jars into $LIB_DIR ..."
# -j flattens the lib/ path; -o overwrites.
unzip -o -j "$zip" 'lib/*.jar' -d "$LIB_DIR" >/dev/null

fetch() {
    url="$1"; out="$2"
    echo "  $out"
    curl -fL --retry 3 -o "$LIB_DIR/$out" "$url"
}

echo "Downloading application dependencies from Maven Central ..."
fetch "$MAVEN/com/google/code/gson/gson/2.8.6/gson-2.8.6.jar" "gson-2.8.6.jar"
fetch "$MAVEN/com/h2database/h2/1.4.196/h2-1.4.196.jar" "h2-1.4.196.jar"
fetch "$MAVEN/net/sourceforge/csvjdbc/csvjdbc/1.0-37/csvjdbc-1.0-37.jar" "csvjdbc-1.0-37.jar"
fetch "$MAVEN/commons-fileupload/commons-fileupload/1.3.3/commons-fileupload-1.3.3.jar" "commons-fileupload-1.3.3.jar"
fetch "$MAVEN/commons-io/commons-io/2.6/commons-io-2.6.jar" "commons-io-2.6.jar"

echo ""
echo "Done: $(ls "$LIB_DIR"/*.jar 2>/dev/null | wc -l) jars in $LIB_DIR"
echo ""
echo "Notes:"
echo "  * SP32 is a newer service pack than the original vendored set, so some bundled OSS"
echo "    jars differ in version/name (e.g. commons-lang3 vs commons-lang). Compile/run against"
echo "    the whole directory (-cp \"lib/*\") rather than hard-coded jar names."
echo "  * SP32 does not ship sap.com~tc~sec~csi.jar or log4j 1.x; they are not required for"
echo "    in-process PDF export. If your report needs them, add them from your own SAP runtime."
