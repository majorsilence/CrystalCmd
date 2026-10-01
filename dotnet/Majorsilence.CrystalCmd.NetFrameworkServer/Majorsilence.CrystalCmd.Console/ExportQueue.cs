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
        // Channel names must match what the server controllers enqueue on
        // (ExportController/AnalyzerController in Majorsilence.CrystalCmd.Server).
        public const string ReportsChannel = "crystal-reports";
        public const string AnalyzerChannel = "crystal-analyzer";

        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task _backgroundTask;
        private readonly string channel;
        private readonly CrystalCmd.Common.IReportExporter _exporter;
        private readonly CrystalCmd.Common.IReportAnalyzer _analyzer;

        /// <summary>A queue worker on the Crystal Reports runtime.</summary>
        public ExportQueue(ILogger logger, string channel)
            : this(logger, channel, new Exporter(logger), new CrystalReportsAnalyzer())
        {
        }

        /// <summary>A queue worker on whichever backend implements the two interfaces.</summary>
        public ExportQueue(ILogger logger, string channel, CrystalCmd.Common.IReportExporter exporter,
            CrystalCmd.Common.IReportAnalyzer analyzer)
        {
            _logger = logger;
            this.channel = channel;
            _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        }

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
                _logger.LogError(ex, "Error while stopping export queue ({Channel})", channel);
            }
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
                bool failed = false;

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
                    failed = true;
                    _logger.LogError(ex, "Error processing queue item ({Channel})", channel);
                }

                if (failed)
                {
                    // Back off before re-dequeuing: the failed item went straight back to
                    // Pending, so without this pause all of its retries burn within a few
                    // seconds and a short-lived transient error (engine warm-up, temp file
                    // contention) permanently parks the item as Failed.
                    await Task.Delay(5000);
                    continue;
                }

                if (!processed)
                {
                    // No items to process, wait a bit
                    await Task.Delay(1000);
                    continue;
                }
            }
        }


        internal async Task<GeneratedReportPoco> ProcessData(QueueItem item, WorkQueue queue)
        {
            string workingDir = System.IO.Path.Combine(Server.Common.WorkingFolder.GetMajorsilenceTempFolder(), item.Id);
            System.IO.Directory.CreateDirectory(workingDir);
            string rptFile = System.IO.Path.Combine(workingDir, $"{item.Id}.rpt");
            System.IO.File.WriteAllBytes(rptFile, item.ReportTemplate);

            try
            {
                if (item.Data != null)
                {
                    // export pdf

                    var output = _exporter.Export(rptFile, item.Data);
                    var bytes = output.Content;
                    var fileExt = output.Extension;
                    var mimeType = output.MediaType;
                    return new GeneratedReportPoco
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
            }
            finally
            {
                // Clean up the uploaded .rpt on every path (export, analysis, or throw);
                // the analyzer branch and the exception path used to leak this folder.
                try
                {
                    System.IO.Directory.Delete(workingDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error deleting working folder {workingDir}: {ex.Message}");
                }
            }
        }
    }
}
