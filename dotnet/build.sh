#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable

cwd=$(pwd)
DOCKER_BUILDKIT=1
version=`cat VERSION_WINE`
echo "version: $version"

echo "build base image"
docker build -f Dockerfile.wine -t majorsilence/dotnet_framework_wine -m 4GB .
docker tag majorsilence/dotnet_framework_wine:latest majorsilence/dotnet_framework_wine:$version
#docker push majorsilence/dotnet_framework_wine:latest
#docker push majorsilence/dotnet_framework_wine:$version

crystal_cmd_version=`cat VERSION_CRYSTALCMD`
echo "build crystal reports service image"
docker build --no-cache --build-arg A_CRYSTALCMD_VERSION="$crystal_cmd_version" -f Dockerfile.crystalcmd -t majorsilence/dotnet_framework_wine_crystalcmd -m 4GB .
docker tag majorsilence/dotnet_framework_wine_crystalcmd:latest majorsilence/dotnet_framework_wine_crystalcmd:$crystal_cmd_version
#docker push majorsilence/dotnet_framework_wine_crystalcmd:latest
#docker push majorsilence/dotnet_framework_wine_crystalcmd:$crystal_cmd_version