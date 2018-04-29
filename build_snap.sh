#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable


snap_build()
{
	wget http://download.oracle.com/otn-pub/java/jdk/8u172-b11/a58eab1ec242421181065cdc37240b08/jre-8u172-linux-x64.tar.gz --user-agent="Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:56.0) Gecko/20100101 Firefox/56.0" --header="Cookie: oraclelicense=accept-securebackup-cookie Accept: text/html"

	snapcraft clean
	snapcraft snap -o "$(pwd)/java/CrystalCmd/build/CrystalCmd.snap"
	# snapcraft clean
	rm -rf ./jre-8u172-linux-x64.tar.gz
}

snap_build

