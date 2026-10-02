using Majorsilence.CrystalCmd.Common;

namespace Majorsilence.CrystalCmd.Routing.Tests;

/// <summary>
/// The serviceable rule on the sample templates, and the router's three inputs in order.
/// </summary>
[TestFixture]
public class RoutingTests
{
    private static byte[] Template(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, name));

    // The five samples with one table or none, and no subreport reading its own table.
    [TestCase("thereport.rpt")]
    [TestCase("thereport_wth_parameters.rpt")]
    [TestCase("the_dotnet_dataset_report.rpt")]
    [TestCase("thereport_with_subreport_with_parameters.rpt")]
    [TestCase("analyzer_report.rpt", false)]
    public void Rule_OnTheSampleTemplates(string name, bool expectedServiceable = true)
    {
        var result = ServiceableRule.Evaluate(new Data(), Template(name));
        Assert.That(result.Serviceable, Is.EqualTo(expectedServiceable), result.ToString());
    }

    [TestCase("thereport_with_subreport_with_dotnet_dataset.rpt")]
    [TestCase("the_dotnet_dataset_report_with_params_and_subreport.rpt")]
    [TestCase("analyzer_report.rpt")]
    public void Rule_RefusesATemplateWhoseSubreportReadsItsOwnTable(string name)
    {
        var result = ServiceableRule.Evaluate(null, Template(name));
        Assert.That(result.Serviceable, Is.False);
        Assert.That(result.Reasons.Single(), Does.Contain("subreport reads its own table"));
    }

    [Test]
    public void Rule_ReadsTheRequestToo_AndNamesEveryReason()
    {
        var data = new Data { ExportAs = ExportTypes.WordDoc };
        data.AddData("A", "X\nString\n\"1\"\n");
        data.AddData("B", "X\nString\n\"1\"\n");
        data.SetEmptyTable("Sub", "T");

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
        var not = BackendRouter.Route(new Data(), Template("thereport_with_subreport_with_dotnet_dataset.rpt"), RenderBackend.Auto);

        Assert.Multiple(() =>
        {
            Assert.That(serviceable.Backend, Is.EqualTo(RenderBackend.RptEngine));
            Assert.That(serviceable.Reason, Does.Contain("serviceable rule"));
            Assert.That(not.Backend, Is.EqualTo(RenderBackend.Crystal));
            Assert.That(not.Reason, Does.Contain("subreport reads its own table"));
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
