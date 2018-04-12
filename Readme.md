# Example usage

Note that this is very slow and highly recommend using windows to generate the reports or to cache the reports after they have been generated.

## command line mode

CrystalCmd upports running as a command line tool. Pass in path to report, data, and output fileand a pdf is generated.

```bash
java -jar CrystalCmd.jar -reportpath "/path/to/report.rpt" -datafile "/path/to/data.json" -outpath "/path/to/generated/file.pdf"
```

example 2

```bash
java -jar CrystalCmd.jar -reportpath "/home/peter/Projects/CrystalCmd/the_dataset_report.rpt" -datafile "/home/peter/Projects/CrystalCmd/test.json" -outpath "/home/peter/Projects/CrystalCmd/java/CrystalCmd/build/output.pdf"
```

## Server mode

CrystalCmd supports running in server mode.  If you runn it with no command line arguments it
starts a web server listending on port 4321.  There are two end points that can be called.

1. http://localhost:4321/status
1. http://localhost:4321/export
    * Returns pdf as bytestream
    * Must be passed two post variables as byte arrays
        * reporttemplate
        * reportdata

Run the server.

```bash
java -jar CrystalCmd.jar
```

Call the server.

```bash
curl -F "reporttemplate=@the_dataset_report.rpt" -F "reportdata=@test.json" http://localhost:4321/export > myoutputfile.pdf
```


### Example of using the installed snap

install
```bash
snap install ./java/CrystalCmd/build/CrystalCmd.snap --force-dangerous --classic
```

run
```bash
crystalcmd -reportpath "/home/peter/Projects/CrystalWrapper/the_dataset_report.rpt" -datafile "/home/peter/Projects/CrystalWrapper/test.json" -outpath "/home/peter/Projects/CrystalWrapper/Java/build/output.pdf"
```

# Building Snaps

```bash
sudo ./build_snap.sh
```


# Java

## dev setup

```bash
sudo add-apt-repository ppa:webupd8team/java
sudo apt-get update
sudo apt-get install oracle-java8-installer
```

http://download.oracle.com/otn-pub/java/jdk/8u151-b12/e758a0de34e24606bca991d704f6dcbf/jre-8u151-linux-x64.tar.gz?AuthParam=1509230467_0755e209172b0f2026ed83c4a73a1ef0

Download [eclipse java edition](http://www.eclipse.org/downloads/eclipse-packages/).

Setup eclipse with [crystal references](https://archive.sap.com/documents/docs/DOC-29757).

Import java folder as ecplise project.

## Runtime setup

```bash
sudo add-apt-repository ppa:webupd8team/java
sudo apt-get update
sudo apt-get install oracle-java8-installer
```

## Export Jar
* Eclipse -> File -> Export -> Java -> Runnable Jar File

Package required libraries into generated JAR

output as "CrystalCmd.jar" in folder ./CrystalCmd/java/CrystalCmd/build


# Dot Net

Use this project to generate test data from c# program

# Crystal report examples

https://wiki.scn.sap.com/wiki/display/BOBJ/Crystal+Reports+Java++SDK+Samples#CrystalReportsJavaSDKSamples-Database
