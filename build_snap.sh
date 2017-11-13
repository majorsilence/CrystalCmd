#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable


snap_build()
{
	wget http://download.oracle.com/otn-pub/java/jdk/8u152-b16/aa0333dd3019491ca4f6ddbe78cdb6d0/jre-8u152-linux-x64.tar.gz --user-agent="Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:56.0) Gecko/20100101 Firefox/56.0" --header="Cookie: oraclelicense=accept-securebackup-cookie Accept: text/html"

	snapcraft clean
	snapcraft snap -o "$(pwd)/Java/build/CrystalCmd.snap"
	# snapcraft clean
	rm -rf ./jre-8u152-linux-x64.tar.gz
}

snap_build

