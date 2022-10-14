FROM ubuntu:20.04

LABEL maintainer "Peter Gill"

# make install files accessible
COPY CrystalCmd.jar /root

WORKDIR /

RUN apt update \
        && apt-get install -y software-properties-common -y \
        && apt update \
        && apt install openjdk-16-jdk ttf-mscorefonts-installer -y \
        && rm -rf /var/lib/apt/lists/*
EXPOSE 4321
CMD ["java", "-jar", "/root/CrystalCmd.jar"]

