using Majorsilence.Crystal.Model;
using Majorsilence.Crystal.Model.Fields;
using Majorsilence.Crystal.Model.Objects;
using Majorsilence.Crystal.Parser;
using Majorsilence.CrystalCmd.Common;

namespace Majorsilence.CrystalCmd.Routing;

/// <summary>What the rule decided, and every reason it said no.</summary>
public sealed class Serviceability
{
    public Serviceability(IReadOnlyList<string> reasons)
    {
        Reasons = reasons;
    }

    public bool Serviceable => Reasons.Count == 0;
    public IReadOnlyList<string> Reasons { get; }

    public override string ToString() => Serviceable ? "serviceable" : string.Join("; ", Reasons);
}

/// <summary>
/// The serviceable rule: whether the Majorsilence.Crystal backend can render a request
/// correctly today. It reads the request (tables, subreport data, export type) and parses
/// the template (how many tables it reads, whether a subreport reads its own, cross-tabs,
/// charts). Parsing a template takes milliseconds, so this runs when the request arrives.
/// The worker applies the request half again when it renders, as the last line of defence.
/// </summary>
public static class ServiceableRule
{
    public static Serviceability Evaluate(Data? data, byte[] template)
    {
        if (template is null) throw new ArgumentNullException(nameof(template));
        var reasons = new List<string>();
        RequestReasons(data, reasons);
        TemplateReasons(template, reasons);
        return new Serviceability(reasons);
    }

    /// <summary>The request half of the rule, with no template needed.</summary>
    public static void RequestReasons(Data? data, List<string> reasons)
    {
        if (data is null) return;

        int tables = data.DataTables.Count + data.EmptyDataTables.Count;
        if (tables > 1)
            reasons.Add($"the request carries {tables} tables and the backend renders from one flattened table");
        if (data.SubReportDataTables.Count > 0 || data.EmptySubReportDataTables.Count > 0)
            reasons.Add("the request pushes data to a subreport, which the backend cannot hand a subreport yet");
        switch (data.ExportAs)
        {
            case ExportTypes.CrystalReport:
            case ExportTypes.TEXT:
            case ExportTypes.WordDoc:
                reasons.Add($"export type {data.ExportAs} has no equivalent on the backend");
                break;
        }
    }

    /// <summary>The template half of the rule: its shape, from a parse.</summary>
    public static void TemplateReasons(byte[] template, List<string> reasons)
    {
        ParseResult parsed;
        try
        {
            using var stream = new MemoryStream(template);
            parsed = RptParser.Parse(stream);
        }
        catch (Exception ex)
        {
            reasons.Add($"the template could not be parsed by the backend: {ex.Message}");
            return;
        }
        if (!parsed.Success || parsed.Report is null)
        {
            reasons.Add($"the template could not be parsed by the backend: {string.Join("; ", parsed.Errors)}");
            return;
        }

        var report = parsed.Report;
        int tableCount = report.Fields.OfType<DatabaseField>()
            .Select(f => f.TableName).Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (tableCount > 1)
            reasons.Add($"the report reads {tableCount} tables and the backend renders from one flattened table");

        var subreportsWithData = AllSubreports(report)
            .Where(s => s.Report is not null && s.Report.Fields.OfType<DatabaseField>().Any())
            .Select(s => s.SubreportName).ToList();
        if (subreportsWithData.Count > 0)
            reasons.Add($"a subreport reads its own table ({string.Join(", ", subreportsWithData)}), which the backend cannot hand data yet");

        if (AllObjects(report).OfType<CrossTabObject>().Any())
            reasons.Add("the report has a cross-tab, whose rendering is not yet measured on the backend");
        if (AllObjects(report).OfType<ChartObject>().Any())
            reasons.Add("the report has a chart, whose rendering is not yet measured on the backend");
    }

    private static IEnumerable<SubreportObject> AllSubreports(ReportDefinition report)
    {
        foreach (var sub in report.Sections.SelectMany(s => s.Objects).OfType<SubreportObject>())
        {
            yield return sub;
            if (sub.Report is not null)
                foreach (var nested in AllSubreports(sub.Report))
                    yield return nested;
        }
    }

    // Every object in the report and its subreports, so a cross-tab or chart inside a
    // subreport counts too.
    private static IEnumerable<ReportObject> AllObjects(ReportDefinition report)
    {
        foreach (var o in report.Sections.SelectMany(s => s.Objects))
            yield return o;
        foreach (var sub in AllSubreports(report))
            if (sub.Report is not null)
                foreach (var o in sub.Report.Sections.SelectMany(s => s.Objects))
                    yield return o;
    }
}
