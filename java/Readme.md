
Used libraries

## Server features & configuration

**Build/runtime:** the project targets **Java 21 (LTS)** and the Docker images use
`eclipse-temurin:21-jre`. It compiles cleanly against the bundled Crystal SDK jars under
JDK 21. Note the Crystal RAS SDK in `lib/` is old; if it raises `InaccessibleObjectException`
or similar at runtime under the JPMS strong encapsulation introduced in Java 17+, add the
relevant `--add-opens java.base/<pkg>=ALL-UNNAMED` flags to the `java` command, or fall back
to a Java 17 JRE.

The Java server now matches the C# server's HTTP surface and security model.

**Endpoints** (all but `/status` and `/healthz` require authentication):

- `GET  /status`, `GET /healthz` — health check (unauthenticated)
- `POST /export` — render synchronously, returns the document bytes
- `POST /export/poll` — enqueue, returns an opaque polling handle
- `GET  /export/poll` — fetch a queued report (`id` header = the handle); `202` while processing
- `POST /analyzer`, `POST /analyzer/poll`, `GET /analyzer/poll` — report structure analysis (JSON)

Rendering supports the full `Data` payload (parameters, sub-report data/parameters, record
selection formula, formula-field text, suppress, can-grow, resize, sort, move objects) and
all export formats (PDF, Excel, Excel-data-only, Word, RTF, Text, CSV, Crystal). Both a
gzip-compressed `StreamedRequest` JSON body and `multipart/form-data` are accepted.

> SDK note: `Data.ObjectText` is not applied — the Java RAS SDK exposes `ITextObject` as
> read-only (unlike .NET's writable `TextObject.Text`). Use a formula field instead.

**Work queue:** results are stored in a JDBC work queue so the polling flow survives long
renders. It defaults to an embedded **H2** database (driver ships in `lib/`); SQLite,
PostgreSQL and SQL Server are also supported but need their JDBC driver jar added to `lib/`
and the `MANIFEST.MF` `Class-Path`.

**Configuration (environment variables):**

| Variable | Purpose | Default |
|---|---|---|
| `CRYSTALCMD_USERNAME` / `CRYSTALCMD_PASSWORD` | Basic auth credentials | _(unset → all requests rejected)_ |
| `CRYSTALCMD_JWT_KEY` / `_ISSUER` / `_AUDIENCE` | JWT (HS256) validation; key must be ≥ 32 bytes | _(JWT disabled)_ |
| `CRYSTALCMD_POLL_TOKEN_KEY` | HMAC key binding poll handles to the caller | falls back to JWT key |
| `CRYSTALCMD_MAX_REQUEST_BODY_BYTES` | request body cap | 104857600 (100 MB) |
| `CRYSTALCMD_MAX_DECOMPRESSED_BYTES` | gzip decompression cap (zip-bomb guard) | 209715200 (200 MB) |
| `CRYSTALCMD_WORKQUEUE_SQLTYPE` | `h2` \| `sqlite` \| `postgresql` \| `sqlserver` | `h2` |
| `CRYSTALCMD_WORKQUEUE_CONNECTION` | JDBC URL for the work queue | embedded H2 file in temp dir |
| `CRYSTALCMD_ALLOW_DEFAULT_CREDENTIALS` | permit `user`/`password` (local testing only) | `false` |
| `CRYSTALCMD_PORT` | listen port | 4321 |

**Security posture (mirrors the C# server):** the server **fails closed** — it rejects every
request until credentials are configured, and **refuses to start** if the credentials are the
well-known `user`/`password` (unless `CRYSTALCMD_ALLOW_DEFAULT_CREDENTIALS=true`). Basic and
JWT credentials are compared in constant time. As with any CrystalCmd deployment, the server
renders untrusted `.rpt` templates — run it as a low-privilege user behind a TLS-terminating,
authenticating proxy with restricted network egress (see the top-level Readme Security section).

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



