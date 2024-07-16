
**CrystalCMD** is a C#/dotnet program that loads JSON files into Crystal Reports to produce PDFs. Initially an experimental proof of concept, it demonstrates generating Crystal Reports on Linux using the .NET framework (wine).

**Key Features:**

- PDF Generation: Converts JSON (and embedded csv) data into PDF reports with Crystal Reports templates.
- Command Line & Server Modes: Supports both modes; server mode is recommended for better performance.
- Cross-Platform: Works on Linux and can run .NET implementations using Wine.

# Client

## Nuget package
```powershell
dotnet add package Majorsilence.CrystalCmd.Client
```

## Curl example

```bash
curl -u "username:password" -F "reportdata=@test.json" -F "reporttemplate=@the_dataset_report.rpt" http://127.0.0.1:4321/export --output testout.pdf
```

## C# code example
```cs
DataTable dt = new DataTable();

// init reprt data
var reportData = new Majorsilence.CrystalCmd.Common.Data()
{
    DataTables = new Dictionary<string, string>(),
    MoveObjectPosition = new List<Majorsilence.CrystalCmd.Common.MoveObjects>(),
    Parameters = new Dictionary<string, object>(),
    SubReportDataTables = new List<Majorsilence.CrystalCmd.Common.SubReports>()
};

// add as many data tables as needed.  The client library will do the necessary conversions to json/csv.
reportData.AddData("report name goes here", "table name goes here", dt);

// export to pdf
var crystalReport = System.IO.File.ReadAllBytes("The rpt template file path goes here");
using (var instream = new MemoryStream(crystalReport))
using (var outstream = new MemoryStream())
{
    var rpt = new Majorsilence.CrystalCmd.Client.Report(serverUrl, username: "The server username goes here", password: "The server password goes here");
    using (var stream = await rpt.GenerateAsync(reportData, instream, _httpClient))
    {
        stream.CopyTo(outstream);
        return outstream.ToArray();
    }
}
```

# Postman Collection

[Majorsilence.CrystalCMD.postman_collection.json](https://github.com/majorsilence/CrystalCmd/blob/main/Majorsilence.CrystalCMD.postman_collection.json)

# Server requirements
- Download the Crystal Reports .net runtime from: [https://origin.softwaredownloads.sap.com/public/site/index.html](https://origin.softwaredownloads.sap.com/public/site/index.html)
  - CR for Visual Studio SP35 CR Runtime 64-bit
  - CR for Visual Studio SP35 CR Runtime 32-bit

- Majorsilence.CrystalCmd.NetFrameworkServer
  - net4.8 webapi project
- Majorsilence.CrystalCmd.NetframeworkConsoleServer
  - an embedio based console app/webserver
  - can be run on Linux using wine

# Server

```bash
docker run -p 44355:44355 -e OVERRIDE_WINEARCH_AS_X64='yes' majorsilence/dotnet_framework_wine_crystalcmd:latest
```

## Build docker images

See Dockerfile.wine, Dockerfile.crystalcmd, and build.sh.


