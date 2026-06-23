#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable

# The Crystal runtime jars are not committed. Reconstruct java/CrystalCmd/lib first with
# scripts/download-crystal-libs.sh, then build the CrystalCmd_jar artifact (e.g. in IntelliJ)
# so it bundles those jars.
ARTIFACT="../java/CrystalCmd/out/artifacts/CrystalCmd_jar/"
if [ ! -d "$ARTIFACT" ]; then
    echo "ERROR: $ARTIFACT not found." >&2
    echo "Run scripts/download-crystal-libs.sh and build the CrystalCmd_jar artifact first." >&2
    exit 1
fi

rm -rf ./CrystalCmd_jar
cp -r "$ARTIFACT" .


#build
#docker build -f Dockerfile -t majorsilence/crystalcmd --rm=true .
docker build -f Dockerfile.alpine --progress plain -t majorsilence/crystalcmd --rm=true .
