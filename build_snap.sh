#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable


snap_build()
{
	wget http://download.oracle.com/otn-pub/java/jdk/8u161-b12/2f38c3b165be4555a1fa6e98c45e0808/jre-8u161-linux-x64.tar.gz --user-agent="Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:56.0) Gecko/20100101 Firefox/56.0" --header="Cookie: oraclelicense=accept-securebackup-cookie Accept: text/html"

	snapcraft clean
	snapcraft snap -o "$(pwd)/java/CrystalCmd/build/CrystalCmd.snap"
	# snapcraft clean
	rm -rf ./jre-8u161-linux-x64.tar.gz
}

snap_build

