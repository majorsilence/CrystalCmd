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
        _backgroundTask = Task.Run(async () =>
        {
            try
            {
                await RunQueue();
            }
            catch (Exception ex)
            {
                // Nothing awaits this task until Stop, so a loop that ends this way would
                // otherwise end without a word while the worker looks alive.
                _logger.LogError(ex, "Queue processor ({Channel}) stopped", Channel);
                throw;
            }
        });
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
        if (!await MigrateUntilReady(queue))
            return;

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

    // A worker starts one loop per core and every loop migrates. On a database the queue has
    // not been created in, their CREATE TABLE IF NOT EXISTS statements race, and PostgreSQL
    // fails all but one of them on a duplicate type. A database not reachable yet fails the
    // same way. Either is retried, so a loop that loses comes up a moment later instead of
    // ending; false only when the loop is stopped first.
    private async Task<bool> MigrateUntilReady(WorkQueue queue)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await queue.Migrate();
                return true;
            }
            catch (Exception ex) when (!_cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogWarning("Queue processor ({Channel}) could not prepare the work queue (attempt {Attempt}), retrying: {Message}",
                    Channel, attempt, ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt, 10)), _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
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
