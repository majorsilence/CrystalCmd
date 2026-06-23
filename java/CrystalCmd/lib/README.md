# java/CrystalCmd/lib

This directory is **intentionally empty in source control**. It holds:

- the SAP Crystal Reports for Eclipse runtime jars (proprietary — must not be redistributed
  in source control), and
- this project's own Java dependencies (gson, H2, csvjdbc, commons-fileupload, commons-io).

The `*.jar` files here are git-ignored. Reconstruct them before building:

```bash
# macOS / Linux
scripts/download-crystal-libs.sh
```
```powershell
# Windows
scripts\download-crystal-libs.ps1
```

The Crystal jars come from SAP's "Crystal Reports for Eclipse SP32 Runtime Libraries"; the
application dependencies come from Maven Central. See `scripts/download-crystal-libs.sh` for
the exact sources and the SAP download portal link.
