using Majorsilence.CrystalCmd.NetframeworkConsole;
using Majorsilence.CrystalCmd.Server.Common;
using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;

namespace Majorsilence.CrystalCmd.Tests
{
    [TestFixture]
    public class ExportQueueTests
    {
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        private static string WorkingDirFor(string id) =>
            Path.Combine(WorkingFolder.GetMajorsilenceTempFolder(), id);

        // The analyzer branch used to return before the cleanup code ran, leaking the
        // uploaded .rpt in the temp folder on every analyze request.
        [Test]
        public void ProcessDataCleansUpWorkingDirOnAnalyzerPath()
        {
            var queue = new ExportQueue(_mockLogger.Object, ExportQueue.AnalyzerChannel);
            string id = Guid.NewGuid().ToString();
            var item = new QueueItem
            {
                Id = id,
                ReportTemplate = File.ReadAllBytes("analyzer_report.rpt"),
                Data = null
            };

            var result = queue.ProcessData(item, null).GetAwaiter().GetResult();

            Assert.Multiple((Action)(() =>
            {
                Assert.That(result.FileContent, Is.Not.Empty);
                Assert.That(Directory.Exists(WorkingDirFor(id)), Is.False,
                    "analyzer working folder should be deleted after processing");
            }));
        }

        // A failing export used to propagate before cleanup, leaking the working folder.
        [Test]
        public void ProcessDataCleansUpWorkingDirWhenExportThrows()
        {
            var queue = new ExportQueue(_mockLogger.Object, ExportQueue.ReportsChannel);
            string id = Guid.NewGuid().ToString();
            var item = new QueueItem
            {
                Id = id,
                ReportTemplate = new byte[] { 1, 2, 3 }, // not a valid .rpt
                Data = new CrystalCmd.Common.Data() { ExportAs = Common.ExportTypes.PDF }
            };

            Assert.CatchAsync<Exception>((AsyncTestDelegate)(async () => await queue.ProcessData(item, null)));
            Assert.That(Directory.Exists(WorkingDirFor(id)), Is.False,
                "working folder should be deleted even when the export fails");
        }

        // Stop() must return promptly and not rethrow, even when the background task
        // faulted (e.g. no queue configuration) or is mid-item; the old implementation
        // waited unbounded and surfaced AggregateException into WinService.OnStop.
        [Test]
        public void StopReturnsPromptlyAndDoesNotThrow()
        {
            var queue = new ExportQueue(_mockLogger.Object, ExportQueue.ReportsChannel);
            queue.Start();
            System.Threading.Thread.Sleep(500);

            var stopwatch = Stopwatch.StartNew();
            Assert.DoesNotThrow((Action)(() => queue.Stop()));
            stopwatch.Stop();

            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(30)));
        }
    }
}
