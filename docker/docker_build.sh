#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable

rm -rf ./CrystalCmd_jar
cp -r ../java/CrystalCmd/out/artifacts/CrystalCmd_jar/ .


#build
#docker build -f Dockerfile -t majorsilence/crystalcmd --rm=true .
docker build -f Dockerfile.alpine --progress plain -t majorsilence/crystalcmd --rm=true .
