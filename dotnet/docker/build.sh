#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable--no-cache 

cwd=$(pwd)

setup_prerequisites() {
    sudo apt install -y docker-buildx docker-compose-v2 skopeo

    #sudo groupadd docker
    # if not in docker group, add user to docker group and change ownership of docker socket
    if ! groups $USER | grep -q "\bdocker\b"; then
        sudo usermod -aG docker $USER
        sudo chown root:docker /var/run/docker.sock
        sudo chown -R root:docker /var/run/dockerUSER
        echo "Group membership changes do not take effect in currently active sessions.  You must log out and log back in for the new group membership to take effect.  Please do so before running this script again."
    fi
}

setup_buildx() {
    builderName="majorsilence-builder"
    if docker buildx inspect "$builderName" >/dev/null 2>&1; then
        docker buildx use "$builderName"
    else
        docker buildx create --name "$builderName" --driver docker-container --use >/dev/null
    fi
    docker buildx inspect "$builderName" --bootstrap
}

build_and_tag_image() {
    local dockerfile_name=$1
    local image_name=$2
    local base_image=$3
    local image_version=$4
    local build_args=$5

    echo "build $image_name:$image_version-$base_image"  
    
    # Use a tag-free OCI layout directory; pass the tag separately in skopeo.
    local folder_name
    folder_name=$(echo "$image_name" | awk -F/ '{print $NF}')

    docker buildx build -f "$dockerfile_name" --progress plain --provenance=true --sbom=true --output "type=oci,name=$image_name:$base_image,oci-mediatypes=true,compression=zstd,force-compression=true,tar=false,dest=$cwd/build/oci/$folder_name" .

    echo "push $image_name image"
    #docker push $image_name:$base_image
    #docker push $image_name:$image_version-$base_image

    skopeo copy --all "oci:$cwd/build/oci/$folder_name:$base_image" "docker://docker.io/$image_name:$base_image"
    skopeo copy --all "oci:$cwd/build/oci/$folder_name:$base_image" "docker://docker.io/$image_name:$image_version-$base_image"
}

#setup_prerequisites
setup_buildx

rm -rf $cwd/build/oci
mkdir -p $cwd/build/oci

version_alpline=`cat VERSION_WINE_ALPINE`
version=`cat VERSION_WINE`
#build_and_tag_image "Dockerfile.wine.alpine" "majorsilence/dotnet_framework_wine" "alpine" "$version_alpline" "A_WINE_VERSION=$version_alpline"
#build_and_tag_image "Dockerfile.wine.ubuntu" "majorsilence/dotnet_framework_wine" "ubuntu" "$version" "A_WINE_VERSION=$version"

crystal_cmd_version=`cat VERSION_CRYSTALCMD`
build_and_tag_image "Dockerfile.crystalcmd.alpine" "majorsilence/dotnet_framework_wine_crystalcmd" "alpine" "$crystal_cmd_version" "A_CRYSTALCMD_VERSION=$crystal_cmd_version"
build_and_tag_image "Dockerfile.crystalcmd.ubuntu" "majorsilence/dotnet_framework_wine_crystalcmd" "ubuntu" "$crystal_cmd_version" "A_CRYSTALCMD_VERSION=$crystal_cmd_version"