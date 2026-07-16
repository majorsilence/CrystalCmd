using Microsoft.Extensions.Logging;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server.Common
{
    public class HealthCheckTask
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task _backgroundTask;
        public static bool IsHealthy { get; private set; } = false;
        private readonly string _rptFilePath;
        private readonly ILogger _logger;
        private readonly bool _failureShouldExitProcess;
        private readonly TimeSpan _checkInterval;

        public HealthCheckTask(ILogger logger, string rptFilePath, bool failureShouldExitProcess,
            TimeSpan? checkInterval = null)
        {
            _logger = logger;
            _rptFilePath = rptFilePath;
            _failureShouldExitProcess = failureShouldExitProcess;
            _checkInterval = checkInterval ?? TimeSpan.FromSeconds(60);
        }

        // Exposed for tests so they can observe that the background task ends on its
        // own after repeated failures instead of deadlocking.
        internal Task BackgroundTask => _backgroundTask;

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
                    var export = new Majorsilence.CrystalCmd.Server.Common.Exporter(_logger);
                    
                    var result = export.exportReportToStream(_rptFilePath, new CrystalCmd.Common.Data()
                    {
                        ExportAs = CrystalCmd.Common.ExportTypes.PDF
                    });

                    
                    IsHealthy = result != null && result.Item1 != null && result.Item1.Length > 1000;
                    _logger.LogInformation("HealthCheckTask: IsHealthy = " + IsHealthy);
                    failCount = 0;
                }
                catch (COMException ex) when (ex.Message.Contains("maximum report processing jobs limit"))
                {
                    IsHealthy = false;
                    failCount++;
                    _logger.LogError(ex, "HealthCheckTask: Error while exporting report to pdf");
                    if (failCount > 5)
                    {
                        // Do NOT call Stop() here: it waits on this very task and deadlocks.
                        _logger.LogError("HealthCheckTask: Too many errors, stopping the process");
                        _cancellationTokenSource.Cancel();
                        if (_failureShouldExitProcess)
                        {
                            System.Environment.Exit(1);
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    IsHealthy = false;
                    failCount++;
                    _logger.LogError(ex, "HealthCheckTask: Error while exporting report to pdf");
                    if (failCount > 1)
                    {
                        // Do NOT call Stop() here: it waits on this very task and deadlocks.
                        _logger.LogError("HealthCheckTask: Too many errors, stopping the process");
                        _cancellationTokenSource.Cancel();
                        if (_failureShouldExitProcess)
                        {
                            System.Environment.Exit(1);
                        }
                        return;
                    }
                }

                // check periodically (default 60 seconds) if export to pdf is still working
                await Task.Delay(_checkInterval, cancellationToken);
            }
        }
    }
}

