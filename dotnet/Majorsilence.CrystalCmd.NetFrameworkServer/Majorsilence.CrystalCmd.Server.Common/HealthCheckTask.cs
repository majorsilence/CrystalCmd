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

        public HealthCheckTask(ILogger logger, string rptFilePath, bool failureShouldExitProcess)
        {
            _logger = logger;
            _rptFilePath = rptFilePath;
            _failureShouldExitProcess = failureShouldExitProcess;
        }

        public void Start()
        {
            _backgroundTask = Task.Run(async () => await DoWorkAsync(_cancellationTokenSource.Token));
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _backgroundTask?.Wait();
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
                    failCount++;
                    _logger.LogError(ex, "HealthCheckTask: Error while exporting report to pdf");
                    if (failCount > 5)
                    {
                        IsHealthy = false;
                        _logger.LogError("HealthCheckTask: Too many errors, stopping the process");
                        Stop();
                        if (_failureShouldExitProcess)
                        {
                            System.Environment.Exit(1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    IsHealthy = false;
                    failCount++;
                    _logger.LogError(ex, "HealthCheckTask: Error while exporting report to pdf");
                    if (failCount > 1)
                   {
                        _logger.LogError("HealthCheckTask: Too many errors, stopping the process");
                        Stop();
                        if (_failureShouldExitProcess)
                        {
                            System.Environment.Exit(1);
                        }
                    }
                }

                // check every 60 seconds if export to pdf is still working
                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
            }
        }
    }
}

