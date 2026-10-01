namespace Majorsilence.CrystalCmd.Common
{
    /// <summary>
    /// Reads a report template's structure: parameters, tables, subreports and report
    /// objects. The analyzer counterpart of <see cref="IReportExporter"/>, and the same
    /// seam for a second backend.
    /// </summary>
    public interface IReportAnalyzer
    {
        FullReportAnalysisResponse Analyze(string reportPath);
        FullReportAnalysisResponse Analyze(byte[] reportTemplate);
    }
}
