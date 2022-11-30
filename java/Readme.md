
Used libraries

## Crystal report examples

https://wiki.scn.sap.com/wiki/display/BOBJ/Crystal+Reports+Java++SDK+Samples#CrystalReportsJavaSDKSamples-Database


## Crystal reports Eclipse JAR library downloads

https://origin.softwaredownloads.sap.com/public/site/index.html


## Crystal Reports and PDF Export ** IMPORTANT - WILL NOT WORK WITHOUT THIS STEP**

### Linux requires fonts installed

```bash
# try the ubuntu or fedora way first
# https://answers.sap.com/questions/676449/nullpointerexception-in-opentypefontmanager.html
apk add --no-cache msttcorefonts-installer && update-ms-fonts && fc-cache -f && ln -s /usr/share/fonts/truetype/msttcorefonts /usr/lib/jvm/default-jvm/jre/lib/fonts

# ubuntu
sudo apt install fonts-dejavu fontconfig ttf-mscorefonts-installer
ln -s /usr/share/fonts/truetype/msttcorefonts /usr/lib/jvm/java-1.11.0-openjdk-amd64/lib/fonts

# fedora
dnf install fontconfig dejavu-sans-fonts dejavu-serif-fonts
```

### Windows 

copy C:\Windows\fonts into the jre/jdk lib/fonts folder.

For example copy 'C:\Windows\fonts' into 'C:\Users\[UserName]\.jdks\[JavaVersion]\lib\fonts'


### Mac


copy '/System/Library/Fonts' into '/Users/[UserName]]/Library/Java/JavaVirtualMachines/[JavaVersion]/Contents/Home/lib/fonts'




## CSV ResultSet Driver
https://sourceforge.net/projects/csvjdbc/

https://sourceforge.net/projects/dans-dbf-lib/files/

https://github.com/simoc/csvjdbc


## GSON

https://github.com/google/gson



