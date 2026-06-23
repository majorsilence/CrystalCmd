FROM eclipse-temurin:21-jre-jammy

LABEL maintainer="Peter Gill"

# make install files accessible
COPY CrystalCmd.jar /root/

WORKDIR /

# Crystal PDF export needs fonts present (see Readme "Crystal Reports and PDF Export").
# Modern JREs resolve fonts via fontconfig, so no JRE lib/fonts symlink is needed.
RUN apt-get update \
    && echo "ttf-mscorefonts-installer msttcorefonts/accepted-mscorefonts-eula select true" | debconf-set-selections \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
        fonts-dejavu fontconfig ttf-mscorefonts-installer \
    && fc-cache -f \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 4321
CMD ["java", "-jar", "/root/CrystalCmd.jar"]
