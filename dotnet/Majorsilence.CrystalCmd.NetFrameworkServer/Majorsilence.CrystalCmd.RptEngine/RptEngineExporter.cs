using Majorsilence.Crystal.RptEngine;
using Majorsilence.Crystal.Runtime;
using Majorsilence.CrystalCmd.Common;
using Microsoft.Extensions.Logging;

namespace Majorsilence.CrystalCmd.RptEngine;

/// <summary>
/// <see cref="IReportExporter"/> on Majorsilence.Crystal's engine: the template is parsed
/// and analyzed, the request translated against that analysis, and the report rendered
/// from the pushed data alone. The engine never opens the connection a template names.
/// </summary>
public sealed class RptEngineExporter : IReportExporter
{
    private static readonly object InitLock = new();
    private static bool s_initialized;

    private readonly ILogger _logger;
    private readonly ReportEngine _engine = new();

    public RptEngineExporter(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        EnsureInitialized();
    }

    public static void EnsureInitialized()
    {
        lock (InitLock)
        {
            if (s_initialized) return;
            ReportEngine.Init();
            s_initialized = true;
        }
    }

    public ReportExport Export(string reportPath, Data data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));

        ReportAnalysis analysis;
        using (var template = File.OpenRead(reportPath))
            analysis = _engine.Analyze(template);

        var translated = RequestTranslator.Translate(data, analysis);
        foreach (var warning in translated.Warnings)
            _logger.LogWarning("{TraceId} {Warning}", data.TraceId, warning);

        ExportResult result;
        using (var template = File.OpenRead(reportPath))
            result = _engine.ExportWithWarningsAsync(template, translated.Overrides, translated.Format).GetAwaiter().GetResult();

        foreach (var warning in result.Warnings)
            _logger.LogWarning("{TraceId} {Warning}", data.TraceId, warning);

        return new ReportExport(result.Bytes, result.Extension, result.MediaType);
    }
}
