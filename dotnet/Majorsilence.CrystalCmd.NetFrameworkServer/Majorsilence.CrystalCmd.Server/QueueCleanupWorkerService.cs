using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server
{
    public class QueueCleanupWorkerService : BackgroundService
    {
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(30);

        readonly IConfiguration _configuration;
        readonly ILogger<QueueCleanupWorkerService> _logger;

        public QueueCleanupWorkerService(IConfiguration configuration, ILogger<QueueCleanupWorkerService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var queue = WorkQueue.CreateDefault("", _configuration);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await queue.GarbageCollection(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // A transient database error here must not reach the host: the default
                    // BackgroundServiceExceptionBehavior is StopHost, so an unhandled
                    // exception would shut the whole server down over a cleanup pass that
                    // could simply run again in 30 minutes.
                    _logger.LogError(ex, "Queue cleanup failed; retrying in {Interval}", CleanupInterval);
                }

                try
                {
                    await Task.Delay(CleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
