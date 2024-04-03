

Use this project to generate test data from c# program

Download the .net runtime from: https://origin.softwaredownloads.sap.com/public/site/index.html

Majorsilence.CrystalCmd.NetFrameworkServer
net4.8 webapi project
Majorsilence.CrystalCmd.NetframeworkConsoleServer
an embedio based console app/webserver
can be run on Linux using wine

# server

```bash
docker run majorsilence/dotnet_framework_wine_crystalcmd:latest
```

## build docker images

See Dockerfile.wine, Dockerfile.crystalcmd, and build.sh.

# client

## nuget package
```powershell
dotnet add package Majorsilence.CrystalCmd.Client
```

## curl example

```bash
curl -u "username:password" -F "reportdata=@test.json" -F "reporttemplate=@the_dataset_report.rpt" http://127.0.0.1:4321/export --output testout.pdf
```

## C# code example
```cs
DataTable dt = new DataTable();

// init reprt data
var reportData = new Majorsilence.CrystalCmd.Client.Data()
{
    DataTables = new Dictionary<string, string>(),
    MoveObjectPosition = new List<Majorsilence.CrystalCmd.Client.MoveObjects>(),
    Parameters = new Dictionary<string, object>(),
    SubReportDataTables = new List<Majorsilence.CrystalCmd.Client.SubReports>()
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
