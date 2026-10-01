using Majorsilence.CrystalCmd.Common;
using Microsoft.Extensions.Logging;

namespace Majorsilence.CrystalCmd.WorkQueues;

/// <summary>
/// Proves a rendering backend is still alive by exporting a sample report to PDF on an
/// interval. After repeated failures it stops itself and, when asked to, exits the
/// process so the host restarts it. Shared by both workers; each hands in its own backend.
/// </summary>
public class BackendHealthCheck
{
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private Task? _backgroundTask;
    public static bool IsHealthy { get; private set; } = false;
    private readonly string _rptFilePath;
    private readonly ILogger _logger;
    private readonly IReportExporter _exporter;
    private readonly bool _failureShouldExitProcess;
    private readonly TimeSpan _checkInterval;

    public BackendHealthCheck(ILogger logger, IReportExporter exporter, string rptFilePath, bool failureShouldExitProcess,
        TimeSpan? checkInterval = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _rptFilePath = rptFilePath;
        _failureShouldExitProcess = failureShouldExitProcess;
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(60);
    }

    // Exposed so tests can observe that the background task ends on its own after
    // repeated failures instead of deadlocking.
    public Task? BackgroundTask => _backgroundTask;

    public void Start()
    {
        _backgroundTask = Task.Run(async () => await DoWorkAsync(_cancellationTokenSource.Token));
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        try
        {
            // Bounded wait: never block forever, and never self-join if a future
            // caller ever invokes Stop() from within the background task itself.
            _backgroundTask?.Wait(TimeSpan.FromSeconds(10));
        }
        catch (AggregateException)
        {
            // The task ended via cancellation, which is exactly what Stop() requested.
        }
    }

    private async Task DoWorkAsync(CancellationToken cancellationToken)
    {
        int failCount = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = _exporter.Export(_rptFilePath, new Data { ExportAs = ExportTypes.PDF });
                // Healthy means a PDF came back, not a size: the Crystal runtime's PDF of the
                // sample is a few kilobytes and Majorsilence.Crystal's under one, and a
                // threshold tuned to either would call the other dead.
                IsHealthy = result?.Content != null && result.Content.Length > 100
                            && result.Content[0] == (byte)'%' && result.Content[1] == (byte)'P'
                            && result.Content[2] == (byte)'D' && result.Content[3] == (byte)'F';
                _logger.LogInformation("HealthCheckTask: IsHealthy = " + IsHealthy);
                failCount = 0;
            }
            catch (Exception ex)
            {
                IsHealthy = false;
                failCount++;
                _logger.LogError(ex, "HealthCheckTask: Error while exporting report to pdf");
                // The Crystal runtime's job-limit error is transient and worth more
                // patience than anything else; it is matched on the message so this class
                // needs no reference to that runtime.
                int allowedFailures = ex.Message.Contains("maximum report processing jobs limit") ? 5 : 1;
                if (failCount > allowedFailures)
                {
                    // Do NOT call Stop() here: it waits on this very task and deadlocks.
                    _logger.LogError("HealthCheckTask: Too many errors, stopping the process");
                    _cancellationTokenSource.Cancel();
                    if (_failureShouldExitProcess)
                    {
                        Environment.Exit(1);
                    }
                    return;
                }
            }

            // check periodically (default 60 seconds) if export to pdf is still working
            await Task.Delay(_checkInterval, cancellationToken);
        }
    }
}
