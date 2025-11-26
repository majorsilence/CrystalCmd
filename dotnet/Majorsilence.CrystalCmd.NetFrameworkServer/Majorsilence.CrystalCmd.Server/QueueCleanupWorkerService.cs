using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server
{
    public class QueueCleanupWorkerService : BackgroundService
    {
        readonly IConfiguration _configuration;
        public QueueCleanupWorkerService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var queue = WorkQueue.CreateDefault("", _configuration);
            // TODO: Implement queue cleanup logic here
            while (!stoppingToken.IsCancellationRequested)
            {
                await queue.GarbageCollection();
                await Task.Delay(TimeSpan.FromMinutes(30));
            }
        }
    }
}
