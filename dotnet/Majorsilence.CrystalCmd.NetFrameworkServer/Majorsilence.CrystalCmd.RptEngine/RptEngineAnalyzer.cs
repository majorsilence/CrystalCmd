using Majorsilence.Crystal.RptEngine;
using Majorsilence.Crystal.Runtime;
using Majorsilence.CrystalCmd.Common;

namespace Majorsilence.CrystalCmd.RptEngine;

/// <summary>
/// <see cref="IReportAnalyzer"/> on Majorsilence.Crystal's parser: the template's
/// parameters, tables, subreports and report objects, in the response shape the Crystal
/// Reports analyzer returns, with no render step.
/// </summary>
public sealed class RptEngineAnalyzer : IReportAnalyzer
{
    private readonly ReportEngine _engine = new();

    public RptEngineAnalyzer()
    {
        RptEngineExporter.EnsureInitialized();
    }

    public FullReportAnalysisResponse Analyze(string reportPath)
    {
        using var template = File.OpenRead(reportPath);
        return Map(_engine.Analyze(template));
    }

    public FullReportAnalysisResponse Analyze(byte[] reportTemplate)
    {
        using var template = new MemoryStream(reportTemplate);
        return Map(_engine.Analyze(template));
    }

    public static FullReportAnalysisResponse Map(ReportAnalysis analysis) => new()
    {
        Parameters = analysis.Parameters.ToList(),
        ParametersExtended = analysis.ParametersExtended.ToDictionary(kv => kv.Key, kv => kv.Value),
        DataTables = analysis.DataTables.Select(MapTable).ToList(),
        SubReports = analysis.Subreports.Select(s => new FullReportAnalysisResponse.FullSubReportAnalysisDto
        {
            SubreportName = s.SubreportName,
            Parameters = s.Parameters.ToList(),
            DataTables = s.DataTables.Select(MapTable).ToList()
        }).ToList(),
        ReportObjects = analysis.ReportObjects.Select(o => new FullReportAnalysisResponse.ReportObjectsDto
        {
            ObjectName = o.ObjectName,
            Width = o.Width,
            TopPosition = o.TopPosition,
            ObjectValue = o.ObjectValue
        }).ToList()
    };

    private static FullReportAnalysisResponse.DataTableAnalysisDto MapTable(DataTableAnalysis t) => new()
    {
        DataTableName = t.TableName,
        ColumnNames = t.ColumnNames.ToList()
    };
}
