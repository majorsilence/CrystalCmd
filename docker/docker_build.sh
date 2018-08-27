#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable


cp ../java/CrystalCmd/build/CrystalCmd.jar .


#build
docker build --no-cache -f Dockerfile -t majorsilence/crystalcmd --rm=true .
