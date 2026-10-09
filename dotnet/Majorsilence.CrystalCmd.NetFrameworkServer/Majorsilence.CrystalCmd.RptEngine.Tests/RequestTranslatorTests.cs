using Majorsilence.Crystal.RptEngine;
using Majorsilence.Crystal.Runtime;
using Majorsilence.CrystalCmd.Common;

namespace Majorsilence.CrystalCmd.RptEngine.Tests;

/// <summary>
/// The request contract read the way the Crystal worker's callers rely on, one rule per
/// test, without any template or engine involved.
/// </summary>
[TestFixture]
public class RequestTranslatorTests
{
    private static ReportAnalysis Analysis(params (string Table, string[] Columns)[] tables) => new()
    {
        Parameters = [],
        ParametersExtended = new Dictionary<string, string>(),
        DataTables = tables.Select(t => new DataTableAnalysis { TableName = t.Table, ColumnNames = t.Columns }).ToList(),
        Subreports = [],
        ReportObjects = []
    };

    private static ReportAnalysis WithSubreports(params string[] names) => new()
    {
        Parameters = [],
        ParametersExtended = new Dictionary<string, string>(),
        DataTables = [],
        Subreports = names.Select(n => new SubreportAnalysis { SubreportName = n, Parameters = ["MyParameter"], DataTables = [] }).ToList(),
        ReportObjects = []
    };

    private const string EmployeeCsv = "EMPLOYEE_ID,LAST_NAME,HIRED\nInt32,String,DateTime\n\"1\",\"Smith\",\"2020-01-02\"\n\"2\",\"\",\"\"\n";

    [Test]
    public void ACsvTable_BecomesATypedDataTable()
    {
        var data = new Data();
        data.AddData("EMPLOYEE", EmployeeCsv);

        var t = RequestTranslator.Translate(data, Analysis(("EMPLOYEE", ["EMPLOYEE_ID", "LAST_NAME", "HIRED"])));

        var dt = t.Overrides.Data!;
        Assert.Multiple(() =>
        {
            Assert.That(dt.Columns["EMPLOYEE_ID"]!.DataType, Is.EqualTo(typeof(int)));
            Assert.That(dt.Columns["HIRED"]!.DataType, Is.EqualTo(typeof(DateTime)));
            Assert.That(dt.Rows.Count, Is.EqualTo(2));
            Assert.That(dt.Rows[0]["EMPLOYEE_ID"], Is.EqualTo(1));
            Assert.That(dt.Rows[0]["HIRED"], Is.EqualTo(new DateTime(2020, 1, 2)));
            Assert.That(dt.Rows[1]["LAST_NAME"], Is.EqualTo(""), "a blank string column is empty, not null");
            Assert.That(dt.Rows[1]["HIRED"], Is.EqualTo(DBNull.Value), "a blank typed column is null");
            Assert.That(t.Warnings, Is.Empty);
        });
    }

    [Test]
    public void ATableNamedByIndex_OrCaseInsensitively_IsNotWarnedAbout_ButAnUnknownNameIs()
    {
        var analysis = Analysis(("EMPLOYEE", ["EMPLOYEE_ID"]));

        var byIndex = new Data(); byIndex.AddData("1", EmployeeCsv);
        var byCase = new Data(); byCase.AddData("employee", EmployeeCsv);
        var unknown = new Data(); unknown.AddData("Orders", EmployeeCsv);

        Assert.Multiple(() =>
        {
            Assert.That(RequestTranslator.Translate(byIndex, analysis).Warnings, Is.Empty);
            Assert.That(RequestTranslator.Translate(byCase, analysis).Warnings, Is.Empty);
            var t = RequestTranslator.Translate(unknown, analysis);
            Assert.That(t.Warnings, Has.Count.EqualTo(1));
            Assert.That(t.Warnings[0], Does.Contain("Orders").And.Contain("EMPLOYEE"));
            Assert.That(t.Overrides.Data, Is.Not.Null, "the data is still pushed; the engine has one table");
        });
    }

    [Test]
    public void AnEmptyTable_TakesTheTemplatesColumns()
    {
        var data = new Data();
        data.SetEmptyTable("EMPLOYEE");

        var t = RequestTranslator.Translate(data, Analysis(("EMPLOYEE", ["EMPLOYEE_ID", "LAST_NAME"])));

        Assert.That(t.Overrides.Data!.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName),
            Is.EqualTo(new[] { "EMPLOYEE_ID", "LAST_NAME" }));
        Assert.That(t.Overrides.Data.Rows.Count, Is.EqualTo(0));
    }

    [Test]
    public void TwoTables_AreRefusedWithTheRule()
    {
        var data = new Data();
        data.AddData("A", EmployeeCsv);
        data.AddData("B", EmployeeCsv);

        var ex = Assert.Throws<NotServiceableException>(() => RequestTranslator.Translate(data, Analysis()));
        Assert.That(ex!.Message, Does.Contain("2 tables"));
    }

    private static ReportAnalysis WithSubreportTables(params (string Subreport, string Table, string[] Columns)[] subreports) => new()
    {
        Parameters = [],
        ParametersExtended = new Dictionary<string, string>(),
        DataTables = [],
        Subreports = subreports.Select(s => new SubreportAnalysis
        {
            SubreportName = s.Subreport,
            Parameters = [],
            DataTables = [new DataTableAnalysis { TableName = s.Table, ColumnNames = s.Columns }]
        }).ToList(),
        ReportObjects = []
    };

    // A subreport table as the client sends it. Data has no AddData overload taking CSV text for
    // a subreport: a string there binds to the IEnumerable<T> overload, as characters.
    private static SubReports SubreportCsv(string reportName, string tableName, string csv) =>
        new() { ReportName = reportName, TableName = tableName, DataTable = csv };

    // A subreport's table is pushed under the name the client gives the subreport, which the
    // Crystal runtime's callers usually write as the imported file's name.
    [Test]
    public void SubreportData_LandsOnTheSubreportTheClientNames()
    {
        var data = new Data();
        data.SubReportDataTables.Add(SubreportCsv("Sub1.rpt", "EMPLOYEE", EmployeeCsv));

        var t = RequestTranslator.Translate(data, WithSubreportTables(("Sub1", "EMPLOYEE", ["EMPLOYEE_ID"]), ("Sub2", "OTHER", ["X"])));

        Assert.That(t.Overrides.SubreportData.Keys, Is.EqualTo(new[] { "Sub1" }));
        var table = t.Overrides.SubreportData["Sub1"];
        Assert.That(table.Rows.Count, Is.EqualTo(2));
        Assert.That(table.Columns["EMPLOYEE_ID"]!.DataType, Is.EqualTo(typeof(int)));
        Assert.That(t.Warnings, Is.Empty);
    }

    [Test]
    public void AnEmptySubreportTable_HasTheSubreportsOwnColumns()
    {
        var data = new Data();
        data.SetEmptyTable("Sub1", "EMPLOYEE");

        var t = RequestTranslator.Translate(data, WithSubreportTables(("Sub1", "EMPLOYEE", ["EMPLOYEE_ID", "LAST_NAME"])));

        var table = t.Overrides.SubreportData["Sub1"];
        Assert.That(table.Rows.Count, Is.EqualTo(0));
        Assert.That(table.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName), Is.EqualTo(new[] { "EMPLOYEE_ID", "LAST_NAME" }));
    }

    // A subreport renders from one flattened table, as the main report does.
    [Test]
    public void TwoTablesForOneSubreport_AreRefused()
    {
        var data = new Data();
        data.SubReportDataTables.Add(SubreportCsv("Sub1", "EMPLOYEE", EmployeeCsv));
        data.SetEmptyTable("Sub1.rpt", "OTHER");

        var ex = Assert.Throws<NotServiceableException>(() =>
            RequestTranslator.Translate(data, WithSubreportTables(("Sub1", "EMPLOYEE", ["EMPLOYEE_ID"]))));
        Assert.That(ex!.Message, Does.Contain("Sub1"));
    }

    [Test]
    public void SubreportData_ForNoSubreportItCanResolve_IsAWarning()
    {
        var data = new Data();
        data.SubReportDataTables.Add(SubreportCsv("Nope", "EMPLOYEE", EmployeeCsv));

        var t = RequestTranslator.Translate(data, WithSubreportTables(("Sub1", "A", ["X"]), ("Sub2", "B", ["Y"])));

        Assert.That(t.Overrides.SubreportData, Is.Empty);
        Assert.That(t.Warnings.Single(), Does.StartWith("SubReportDataTables: no subreport named 'Nope'"));
    }

    [TestCase(ExportTypes.PDF, ExportFormat.Pdf)]
    [TestCase(ExportTypes.CSV, ExportFormat.Csv)]
    [TestCase(ExportTypes.Excel, ExportFormat.Excel)]
    [TestCase(ExportTypes.ExcelDataOnly, ExportFormat.ExcelDataOnly)]
    [TestCase(ExportTypes.RichText, ExportFormat.Rtf)]
    public void ExportTypes_WithAnEquivalent_Map(ExportTypes exportAs, ExportFormat expected)
    {
        Assert.That(RequestTranslator.MapFormat(exportAs), Is.EqualTo(expected));
    }

    [TestCase(ExportTypes.CrystalReport)]
    [TestCase(ExportTypes.TEXT)]
    [TestCase(ExportTypes.WordDoc)]
    public void ExportTypes_WithoutAnEquivalent_AreRefused(ExportTypes exportAs)
    {
        Assert.Throws<NotServiceableException>(() => RequestTranslator.MapFormat(exportAs));
    }

    [Test]
    public void Moves_MapAxisAmountAndRelative()
    {
        var data = new Data();
        data.MoveObjectPosition.Add(new MoveObjects { ObjectName = "Text1", Move = 100, Type = MoveType.ABSOLUTE, Pos = MovePosition.TOP });
        data.MoveObjectPosition.Add(new MoveObjects { ObjectName = "Text2", Move = -5, Type = MoveType.RELATIVE, Pos = MovePosition.LEFT });

        var moves = RequestTranslator.Translate(data, Analysis()).Overrides.MoveObjectPosition;

        Assert.Multiple(() =>
        {
            Assert.That(moves[0].ObjectName, Is.EqualTo("Text1"));
            Assert.That(moves[0].Axis, Is.EqualTo(MoveAxis.Top));
            Assert.That(moves[0].Amount, Is.EqualTo(100));
            Assert.That(moves[0].Relative, Is.False);
            Assert.That(moves[1].Axis, Is.EqualTo(MoveAxis.Left));
            Assert.That(moves[1].Amount, Is.EqualTo(-5));
            Assert.That(moves[1].Relative, Is.True);
        });
    }

    [Test]
    public void TheRest_IsCopiedAcross_AndSortByTakesTableDotField()
    {
        var data = new Data
        {
            Parameters = { ["Region"] = "West", ["Year"] = 2020 },
            Suppress = { ["Text1"] = true },
            CanGrow = { ["Text2"] = true },
            Resize = { ["Text3"] = 1440 },
            ObjectText = { ["Text4"] = "hello" },
            FormulaFieldText = { ["@f"] = "1 + 1" },
            SortByField = { ["EMPLOYEE"] = "LAST_NAME" },
            RecordSelectionFormula = "{EMPLOYEE.EMPLOYEE_ID} > 0"
        };

        var o = RequestTranslator.Translate(data, Analysis()).Overrides;

        Assert.Multiple(() =>
        {
            Assert.That(o.Parameters["Region"], Is.EqualTo("West"));
            Assert.That(o.Parameters["Year"], Is.EqualTo(2020));
            Assert.That(o.Suppress["Text1"], Is.True);
            Assert.That(o.CanGrow["Text2"], Is.True);
            Assert.That(o.Resize["Text3"], Is.EqualTo(1440));
            Assert.That(o.ObjectText["Text4"], Is.EqualTo("hello"));
            Assert.That(o.FormulaFieldText["@f"], Is.EqualTo("1 + 1"));
            Assert.That(o.SortByFieldName, Is.EqualTo("EMPLOYEE.LAST_NAME"));
            Assert.That(o.RecordSelectionFormula, Is.EqualTo("{EMPLOYEE.EMPLOYEE_ID} > 0"));
        });
    }

    [Test]
    public void SubreportParameters_ResolveTheClientsName_ToTheTemplatesName()
    {
        var values = new Dictionary<string, object> { ["MyParameter"] = "x" };

        var exact = new Data { SubReportParameters = { new SubReportParameters { ReportName = "Sub1", Parameters = values } } };
        var withExtension = new Data { SubReportParameters = { new SubReportParameters { ReportName = "Sub1.rpt", Parameters = values } } };
        var onlyOne = new Data { SubReportParameters = { new SubReportParameters { ReportName = "the_dotnet_dataset_report_with_params", Parameters = values } } };
        var none = new Data { SubReportParameters = { new SubReportParameters { ReportName = "Nope", Parameters = values } } };

        Assert.Multiple(() =>
        {
            Assert.That(RequestTranslator.Translate(exact, WithSubreports("Sub1")).Overrides.SubreportParameters.Keys, Is.EqualTo(new[] { "Sub1" }));
            Assert.That(RequestTranslator.Translate(withExtension, WithSubreports("Sub1")).Overrides.SubreportParameters.Keys, Is.EqualTo(new[] { "Sub1" }));

            var t = RequestTranslator.Translate(onlyOne, WithSubreports("Subreport1"));
            Assert.That(t.Overrides.SubreportParameters.Keys, Is.EqualTo(new[] { "Subreport1" }), "the only subreport gets it");
            Assert.That(t.Warnings, Has.Count.EqualTo(1));

            var miss = RequestTranslator.Translate(none, WithSubreports("Sub1", "Sub2"));
            Assert.That(miss.Overrides.SubreportParameters, Is.Empty);
            Assert.That(miss.Warnings, Has.Count.EqualTo(1));
            Assert.That(miss.Warnings[0], Does.Contain("Nope"));
        });
    }
}
