using Majorsilence.CrystalCmd.Common;

namespace Majorsilence.CrystalCmd.Routing.Tests;

/// <summary>
/// The serviceable rule on the sample templates, and the router's three inputs in order.
/// </summary>
[TestFixture]
public class RoutingTests
{
    private static byte[] Template(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, name));

    // Every sample reads one table or none, and so does each of its subreports.
    [TestCase("thereport.rpt")]
    [TestCase("thereport_wth_parameters.rpt")]
    [TestCase("the_dotnet_dataset_report.rpt")]
    [TestCase("thereport_with_subreport_with_parameters.rpt")]
    [TestCase("thereport_with_subreport_with_dotnet_dataset.rpt")]
    [TestCase("the_dotnet_dataset_report_with_params_and_subreport.rpt")]
    [TestCase("analyzer_report.rpt")]
    public void Rule_OnTheSampleTemplates(string name)
    {
        var result = ServiceableRule.Evaluate(new Data(), Template(name));
        Assert.That(result.Serviceable, Is.True, result.ToString());
    }

    // A subreport renders from one flattened table. No sample has a subreport that reads two,
    // so this takes one from majorsilence.crystal's public corpus when it is checked out
    // beside this repository.
    [Test]
    public void Rule_RefusesATemplateWhoseSubreportReadsSeveralTables()
    {
        string path = Path.GetFullPath("../../../../../../../majorsilence.crystal/tests/rpt-corpus/benbrahim777__Top5USAsubCanada.rpt", AppContext.BaseDirectory);
        Assume.That(File.Exists(path), Is.True, "needs a majorsilence.crystal checkout with its corpus downloaded, beside this one");

        var result = ServiceableRule.Evaluate(null, File.ReadAllBytes(path));

        Assert.That(result.Serviceable, Is.False);
        Assert.That(result.ToString(), Does.Contain("subreport Subreport1 reads 2 tables"));
    }

    [Test]
    public void Rule_ReadsTheRequestToo_AndNamesEveryReason()
    {
        var data = new Data { ExportAs = ExportTypes.WordDoc };
        data.AddData("A", "X\nString\n\"1\"\n");
        data.AddData("B", "X\nString\n\"1\"\n");
        data.SetEmptyTable("Sub", "T");
        data.SubReportDataTables.Add(new SubReports { ReportName = "Sub.rpt", TableName = "U", DataTable = "X\nString\n\"1\"\n" });

        var result = ServiceableRule.Evaluate(data, Template("thereport.rpt"));

        Assert.That(result.Serviceable, Is.False);
        Assert.That(result.Reasons, Has.Count.EqualTo(3));
        Assert.That(result.ToString(), Does.Contain("2 tables").And.Contain("subreport").And.Contain("WordDoc"));
    }

    [Test]
    public void Rule_RefusesWhatItCannotParse()
    {
        var result = ServiceableRule.Evaluate(new Data(), new byte[] { 1, 2, 3 });
        Assert.That(result.Serviceable, Is.False);
        Assert.That(result.Reasons.Single(), Does.Contain("could not be parsed"));
    }

    [Test]
    public void Router_TheRequestsChoiceComesFirst()
    {
        var template = Template("thereport.rpt");

        var crystal = BackendRouter.Route(new Data { Backend = RenderBackend.Crystal }, template, RenderBackend.RptEngine);
        var rptEngine = BackendRouter.Route(new Data { Backend = RenderBackend.RptEngine }, template, RenderBackend.Crystal);

        Assert.Multiple(() =>
        {
            Assert.That(crystal.Backend, Is.EqualTo(RenderBackend.Crystal));
            Assert.That(crystal.ReportsChannel, Is.EqualTo("crystal-reports"));
            Assert.That(crystal.AnalyzerChannel, Is.EqualTo("crystal-analyzer"));
            Assert.That(crystal.Reason, Does.Contain("the request"));
            Assert.That(rptEngine.Backend, Is.EqualTo(RenderBackend.RptEngine));
            Assert.That(rptEngine.ReportsChannel, Is.EqualTo("rptengine-reports"));
            Assert.That(rptEngine.AnalyzerChannel, Is.EqualTo("rptengine-analyzer"));
        });
    }

    [Test]
    public void Router_ThenTheServerDefault()
    {
        var template = Template("thereport.rpt");

        Assert.Multiple(() =>
        {
            Assert.That(BackendRouter.Route(new Data(), template, RenderBackend.Crystal).Backend, Is.EqualTo(RenderBackend.Crystal));
            Assert.That(BackendRouter.Route(new Data(), template, RenderBackend.RptEngine).Backend, Is.EqualTo(RenderBackend.RptEngine));
            Assert.That(BackendRouter.Route(null, template, RenderBackend.RptEngine).Backend, Is.EqualTo(RenderBackend.RptEngine), "an analysis has no request");
            Assert.That(BackendRouter.Route(new Data(), template, RenderBackend.Crystal).Reason, Does.Contain("the server default"));
        });
    }

    [Test]
    public void Router_AutoFollowsTheRule_AndSaysWhy()
    {
        var serviceable = BackendRouter.Route(new Data(), Template("the_dotnet_dataset_report.rpt"), RenderBackend.Auto);
        var twoTables = new Data();
        twoTables.AddData("A", "X\nString\n\"1\"\n");
        twoTables.AddData("B", "X\nString\n\"1\"\n");
        var not = BackendRouter.Route(twoTables, Template("the_dotnet_dataset_report.rpt"), RenderBackend.Auto);

        Assert.Multiple(() =>
        {
            Assert.That(serviceable.Backend, Is.EqualTo(RenderBackend.RptEngine));
            Assert.That(serviceable.Reason, Does.Contain("serviceable rule"));
            Assert.That(not.Backend, Is.EqualTo(RenderBackend.Crystal));
            Assert.That(not.Reason, Does.Contain("2 tables"));
        });
    }

    [Test]
    public void Router_RefusesAnExplicitRptEngineRequestTheRuleRejects_NamingTheRule()
    {
        var data = new Data { Backend = RenderBackend.RptEngine, ExportAs = ExportTypes.TEXT };

        var ex = Assert.Throws<BackendRefusedException>(() => BackendRouter.Route(data, Template("thereport.rpt"), RenderBackend.Crystal));

        Assert.That(ex!.Message, Does.Contain("RptEngine").And.Contain("TEXT"));
        Assert.That(ex.Serviceability.Reasons, Has.Count.EqualTo(1));
    }

    [TestCase(null, RenderBackend.Crystal)]
    [TestCase("", RenderBackend.Crystal)]
    [TestCase("nonsense", RenderBackend.Crystal)]
    [TestCase("crystal", RenderBackend.Crystal)]
    [TestCase("RptEngine", RenderBackend.RptEngine)]
    [TestCase("AUTO", RenderBackend.Auto)]
    public void TheDefaultSetting_ParsesCaseInsensitively_AndFallsBackToCrystal(string? setting, RenderBackend expected)
    {
        Assert.That(BackendRouter.ParseDefault(setting), Is.EqualTo(expected));
    }
}
