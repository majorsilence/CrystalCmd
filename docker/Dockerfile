FROM ubuntu:18.04

LABEL maintainer "Peter Gill"

# make install files accessible
COPY CrystalCmd.jar /root

WORKDIR /

RUN apt update \
        && apt-get install -y software-properties-common -y \
        && add-apt-repository ppa:webupd8team/java -y \
        && apt update \
        && echo "oracle-java8-installer shared/accepted-oracle-license-v1-1 select true" | debconf-set-selections \
        && apt install oracle-java8-installer -y \
        && rm -rf /var/lib/apt/lists/*
EXPOSE 4321
CMD ["java", "-jar", "/root/CrystalCmd.jar"]

