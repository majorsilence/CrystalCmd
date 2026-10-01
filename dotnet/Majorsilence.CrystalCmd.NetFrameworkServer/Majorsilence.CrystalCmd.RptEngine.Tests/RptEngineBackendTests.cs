using System.Data;
using Majorsilence.CrystalCmd.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace Majorsilence.CrystalCmd.RptEngine.Tests;

/// <summary>
/// The backend end to end on the sample templates the Crystal worker's tests use: a
/// request in, a document out, with no SAP runtime on the machine.
/// </summary>
[TestFixture]
public class RptEngineBackendTests
{
    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, name);

    private static DataTable Employees()
    {
        var dt = new DataTable();
        dt.Columns.Add("EMPLOYEE_ID", typeof(int));
        dt.Columns.Add("LAST_NAME", typeof(string));
        dt.Rows.Add(1, "ZZZ-PUSHED-SMITH");
        dt.Rows.Add(2, "ZZZ-PUSHED-JONES");
        return dt;
    }

    private static void AssertPdf(ReportExport export)
    {
        Assert.That(export.Extension, Is.EqualTo("pdf"));
        Assert.That(export.MediaType, Is.EqualTo("application/pdf"));
        // thereport.rpt is one text object; its PDF is under a kilobyte on this engine.
        Assert.That(export.Content.Length, Is.GreaterThan(500));
        Assert.That(System.Text.Encoding.ASCII.GetString(export.Content, 0, 5), Is.EqualTo("%PDF-"));
        Assert.That(System.Text.Encoding.ASCII.GetString(export.Content, export.Content.Length - 32, 32), Does.Contain("%%EOF"));
    }

    [Test]
    public void Export_DatasetReport_WithPushedTable_IsAPdf()
    {
        var data = new Data();
        data.AddData("EMPLOYEE", Employees());

        var export = new RptEngineExporter(NullLogger.Instance).Export(Sample("the_dotnet_dataset_report.rpt"), data);

        AssertPdf(export);
    }

    [Test]
    public void Export_PlainReport_WithNoData_IsAPdf()
    {
        var export = new RptEngineExporter(NullLogger.Instance).Export(Sample("thereport.rpt"), new Data());
        AssertPdf(export);
    }

    [Test]
    public void Export_ParameterReport_TakesTheParameters()
    {
        var data = new Data();
        data.Parameters.Add("MyParameter", "My First Parameter");
        data.Parameters.Add("MyParameter2", true);

        var export = new RptEngineExporter(NullLogger.Instance).Export(Sample("thereport_wth_parameters.rpt"), data);

        AssertPdf(export);
    }

    [TestCase(ExportTypes.CSV, "csv")]
    [TestCase(ExportTypes.RichText, "rtf")]
    [TestCase(ExportTypes.Excel, "xlsx")]
    [TestCase(ExportTypes.ExcelDataOnly, "xlsx")]
    public void Export_OtherFormats_CarryTheirExtension(ExportTypes exportAs, string extension)
    {
        var data = new Data { ExportAs = exportAs };
        data.AddData("EMPLOYEE", Employees());

        var export = new RptEngineExporter(NullLogger.Instance).Export(Sample("the_dotnet_dataset_report.rpt"), data);

        Assert.That(export.Extension, Is.EqualTo(extension));
        Assert.That(export.Content.Length, Is.GreaterThan(50));
        if (exportAs == ExportTypes.CSV)
            Assert.That(System.Text.Encoding.UTF8.GetString(export.Content), Does.Contain("ZZZ-PUSHED-SMITH"));
    }

    [Test]
    public void Export_AnUnsupportedType_IsRefusedBeforeRendering()
    {
        var data = new Data { ExportAs = ExportTypes.TEXT };
        Assert.Throws<NotServiceableException>(() =>
            new RptEngineExporter(NullLogger.Instance).Export(Sample("thereport.rpt"), data));
    }

    [Test]
    public void Analyze_ReportWithSubreport_ListsParametersTablesAndSubreports()
    {
        var response = new RptEngineAnalyzer().Analyze(Sample("the_dotnet_dataset_report_with_params_and_subreport.rpt"));

        Assert.Multiple(() =>
        {
            Assert.That(response.Parameters, Is.EquivalentTo(new[] { "MyParameter", "MyParameter2" }));
            Assert.That(response.ParametersExtended.Keys, Does.Contain("MyParameter"));
            var table = response.DataTables.Single();
            Assert.That(table.DataTableName, Is.EqualTo("EMPLOYEE"));
            Assert.That(table.ColumnNames, Does.Contain("EMPLOYEE_ID"));
            var sub = response.SubReports.Single();
            Assert.That(sub.Parameters, Does.Contain("MyParameter"));
            Assert.That(sub.DataTables.Single().ColumnNames, Does.Contain("FIRST_NAME"));
            Assert.That(response.ReportObjects, Is.Not.Empty);
        });

        var fromBytes = new RptEngineAnalyzer().Analyze(File.ReadAllBytes(Sample("the_dotnet_dataset_report_with_params_and_subreport.rpt")));
        Assert.That(fromBytes.Parameters, Is.EquivalentTo(response.Parameters));
    }
}
