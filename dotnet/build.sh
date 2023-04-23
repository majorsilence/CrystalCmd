#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable

cwd=$(pwd)

echo "build base image"
docker build --progress plain -f Dockerfile.wine -t majorsilence/dotnet_framework_wine -m 4GB .

echo "build crystal reports service image"
docker build --progress plain --no-cache --build-arg A_CRYSTALCMD_VERSION="1.0.13.0" -f Dockerfile.crystalcmd -t majorsilence/dotnet_framework_wine_crystalcmd -m 4GB .
