FROM ubuntu:20.04

LABEL maintainer "Peter Gill"

# make install files accessible
COPY CrystalCmd.jar /root

WORKDIR /

RUN apt update \
        && apt-get install -y software-properties-common -y \
        && apt update \
        && echo ttf-mscorefonts-installer msttcorefonts/accepted-mscorefonts-eula select true | debconf-set-selections \
        && LC_ALL=C DEBIAN_FRONTEND=noninteractive apt install -y openjdk-11-jre fonts-dejavu fontconfig ttf-mscorefonts-installer \
        && fc-cache -f -v \
        && ln -s /usr/share/fonts/truetype/msttcorefonts /usr/lib/jvm/java-1.11.0-openjdk-amd64/lib/fonts
        && rm -rf /var/lib/apt/lists/*
EXPOSE 4321
CMD ["java", "-jar", "/root/CrystalCmd.jar"]

