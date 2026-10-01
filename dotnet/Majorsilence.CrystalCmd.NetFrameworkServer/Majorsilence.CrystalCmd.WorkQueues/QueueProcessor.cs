using Majorsilence.CrystalCmd.Common;
using Microsoft.Extensions.Logging;

namespace Majorsilence.CrystalCmd.WorkQueues;

/// <summary>
/// Dequeues work items from one channel and runs each through a rendering backend: an
/// export when the item carries request data, an analysis when it does not. The backend
/// is whatever implements <see cref="IReportExporter"/> and <see cref="IReportAnalyzer"/>,
/// so the Crystal Reports worker (.NET Framework) and the Majorsilence.Crystal worker
/// (.NET) share this loop and differ only in what they are given.
/// </summary>
public class QueueProcessor
{
    private readonly ILogger _logger;
    private readonly IReportExporter _exporter;
    private readonly IReportAnalyzer _analyzer;
    private readonly Func<WorkQueue> _queueFactory;
    private readonly string _workingRoot;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private Task? _backgroundTask;

    /// <param name="queueFactory">Creates the queue for <paramref name="channel"/>; called once, when the loop starts.</param>
    /// <param name="workingRoot">Where each item's template is written while it is processed, under a folder named by its id.</param>
    public QueueProcessor(ILogger logger, string channel, IReportExporter exporter, IReportAnalyzer analyzer,
        Func<WorkQueue> queueFactory, string workingRoot)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _queueFactory = queueFactory ?? throw new ArgumentNullException(nameof(queueFactory));
        _workingRoot = workingRoot ?? throw new ArgumentNullException(nameof(workingRoot));
    }

    public string Channel { get; }

    public void Start()
    {
        _backgroundTask = Task.Run(async () => await RunQueue());
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        try
        {
            // Bounded wait: RunQueue only observes cancellation between dequeues,
            // so an in-flight report could otherwise block service shutdown
            // indefinitely (the SCM only allows ~30s for OnStop).
            _backgroundTask?.Wait(TimeSpan.FromSeconds(25));
        }
        catch (AggregateException ex)
        {
            _logger.LogError(ex, "Error while stopping queue processor ({Channel})", Channel);
        }
    }

    public async Task RunQueue()
    {
        var queue = _queueFactory();
        await queue.Migrate();

        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            bool processed = false;

            try
            {
                await queue.Dequeue(async (item) =>
                {
                    var report = await ProcessData(item.PayloadAsQueueItem, queue);
                    processed = true;
                    return report;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing queue item: {ex.Message}");
            }

            if (!processed)
            {
                // No items to process, wait a bit
                await Task.Delay(1000);
            }
        }
    }

    public async Task<GeneratedReportPoco> ProcessData(QueueItem item, WorkQueue? queue)
    {
        string workingDir = Path.Combine(_workingRoot, item.Id);
        Directory.CreateDirectory(workingDir);
        string rptFile = Path.Combine(workingDir, $"{item.Id}.rpt");
        File.WriteAllBytes(rptFile, item.ReportTemplate);

        try
        {
            if (item.Data != null)
            {
                var output = _exporter.Export(rptFile, item.Data);
                return new GeneratedReportPoco
                {
                    Id = item.Id,
                    FileContent = output.Content,
                    Format = output.Extension,
                    Metadata = output.MediaType,
                    FileName = $"{item.Id}.{output.Extension}",
                    GeneratedUtc = DateTime.UtcNow
                };
            }

            var response = _analyzer.Analyze(rptFile);
            return new GeneratedReportPoco
            {
                Id = item.Id,
                GeneratedUtc = DateTime.UtcNow,
                FileName = $"{item.Id}_analysis.json",
                Format = "json",
                FileContent = System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(response)),
                Metadata = "application/json"
            };
        }
        finally
        {
            // Clean up the uploaded .rpt on every path (export, analysis, or throw).
            try
            {
                Directory.Delete(workingDir, true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting working folder {workingDir}: {ex.Message}");
            }
        }
    }
}
