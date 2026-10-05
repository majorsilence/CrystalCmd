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
    private readonly PaperSize? _printerPaper;

    /// <param name="printerPaper">
    /// The paper the Crystal host's printer holds. A template that names no paper of its own
    /// prints, in Crystal, on that printer's default paper; given here, such a report is laid
    /// out on it too. Null lays it out on the page it was designed on. A template that names
    /// its own paper is never changed. See <see cref="ParsePrinterPaper"/>.
    /// </param>
    public RptEngineExporter(ILogger logger, PaperSize? printerPaper = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _printerPaper = printerPaper;
        EnsureInitialized();
    }

    /// <summary>
    /// Reads the <c>Worker:PrinterPaper</c> setting: <c>Letter</c>, <c>A4</c> or <c>Legal</c>,
    /// case-insensitive, or empty for none. Anything else is an error, so a misspelt paper
    /// stops the worker rather than being ignored.
    /// </summary>
    public static PaperSize? ParsePrinterPaper(string? setting)
    {
        if (string.IsNullOrWhiteSpace(setting)) return null;
        return setting.Trim().ToUpperInvariant() switch
        {
            "LETTER" => PaperSize.Letter,
            "A4" => PaperSize.A4,
            "LEGAL" => PaperSize.Legal,
            _ => throw new ArgumentException(
                $"Worker:PrinterPaper is '{setting}'; it takes Letter, A4 or Legal, or is left empty.", nameof(setting))
        };
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
        translated.Overrides.PrinterPaper = _printerPaper;
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
