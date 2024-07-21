#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable

cwd=$(pwd)
DOCKER_BUILDKIT=1

build_and_tag_image() {
    local dockerfile_name=$1
    local image_name=$2
    local base_image=$3
    local image_version=$4
    local build_args=$5

    echo "build $image_name:$image_version-$base_image"
    docker build --progress="plain" --build-arg="$build_args" --no-cache -f $dockerfile_name -t $image_name:$base_image -m 4GB .
    docker tag $image_name:$base_image $image_name:$image_version-$base_image

    echo "push $image_name image"
    #docker push $image_name:$base_image
    #docker push $image_name:$image_version-$base_image
}

version=`cat VERSION_WINE`
build_and_tag_image "Dockerfile.wine.alpine" "majorsilence/dotnet_framework_wine" "alpine" "$version" "A_WINE_VERSION=$version"
build_and_tag_image "Dockerfile.wine.ubuntu" "majorsilence/dotnet_framework_wine" "ubuntu" "$version" "A_WINE_VERSION=$version"

crystal_cmd_version=`cat VERSION_CRYSTALCMD`
build_and_tag_image "Dockerfile.crystalcmd.alpine" "majorsilence/dotnet_framework_wine_crystalcmd" "alpine" "$crystal_cmd_version" "A_CRYSTALCMD_VERSION=$crystal_cmd_version"
build_and_tag_image "Dockerfile.crystalcmd.ubuntu" "majorsilence/dotnet_framework_wine_crystalcmd" "ubuntu" "$crystal_cmd_version" "A_CRYSTALCMD_VERSION=$crystal_cmd_version"