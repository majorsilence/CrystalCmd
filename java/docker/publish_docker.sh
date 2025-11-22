#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable

version=`cat VERSION`
echo "version: $version"

docker tag majorsilence/crystalcmd:latest majorsilence/crystalcmd:$version

docker push majorsilence/crystalcmd:latest
docker push majorsilence/crystalcmd:$version