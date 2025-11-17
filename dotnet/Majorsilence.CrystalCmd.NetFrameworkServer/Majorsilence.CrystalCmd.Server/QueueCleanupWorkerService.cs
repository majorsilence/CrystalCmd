using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server
{
    public class QueueCleanupWorkerService : BackgroundService
    {
        public QueueCleanupWorkerService() { }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var queue = WorkQueue.CreateDefault("");
            // TODO: Implement queue cleanup logic here
            while (!stoppingToken.IsCancellationRequested)
            {
                await queue.GarbageCollection();
                await Task.Delay(TimeSpan.FromMinutes(30));
            }
        }
    }
}
