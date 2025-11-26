using ChoETL;
using Majorsilence.CrystalCmd.Server.Common;
using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.NetframeworkConsole
{
    internal class ExportQueue
    {
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task _backgroundTask;
        private readonly string channel;
        public ExportQueue(ILogger logger, string channel)
        {
            _logger = logger;
            this.channel = channel;
        }

        public void Start()
        {
            _backgroundTask = Task.Run(async () => await RunQueue());
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _backgroundTask?.Wait();
        }

        public static List<ExportQueue> Create(ILogger logger, string channel, int threadCount=1)
        {
            var queues = new List<ExportQueue>();
            for (int i = 0; i < threadCount; i++)
            {
                var exportQueue = new ExportQueue(logger, channel);
                queues.Add(exportQueue);
            }

            return queues;
        }

        internal async Task RunQueue()
        {
            var queue = WorkQueue.CreateDefault(channel);
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
                    continue;
                }
            }
        }


        async Task<GeneratedReportPoco> ProcessData(QueueItem item, WorkQueue queue)
        {

            var exporter = new Majorsilence.CrystalCmd.Server.Common.Exporter(_logger);

            string workingDir = System.IO.Path.Combine(Server.Common.WorkingFolder.GetMajorsilenceTempFolder(), item.Id);
            System.IO.Directory.CreateDirectory(workingDir);
            string rptFile = System.IO.Path.Combine(workingDir, $"{item.Id}.rpt");
            System.IO.File.WriteAllBytes(rptFile, item.ReportTemplate);

            GeneratedReportPoco poco = null;

            if (item.Data != null)
            {
                // export pdf

                var output = exporter.exportReportToStream(rptFile, item.Data);
                var bytes = output.Item1;
                var fileExt = output.Item2;
                var mimeType = output.Item3;
                poco = new GeneratedReportPoco
                {
                    Id = item.Id,
                    FileContent = bytes,
                    Format = fileExt,
                    Metadata = mimeType,
                    FileName = $"{item.Id}.{fileExt}",
                    GeneratedUtc = DateTime.UtcNow
                };
            }
            else
            {
                // report analysis
                var analyzer = new CrystalReportsAnalyzer();
                var response = analyzer.GetFullAnalysis(rptFile);
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

            try
            {
                System.IO.Directory.Delete(workingDir, true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting working folder {workingDir}: {ex.Message}");
            }

            return poco;
        }
    }
}
