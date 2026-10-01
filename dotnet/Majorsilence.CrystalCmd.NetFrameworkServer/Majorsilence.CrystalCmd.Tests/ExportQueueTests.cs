using Majorsilence.CrystalCmd.NetframeworkConsole;
using Majorsilence.CrystalCmd.Common;
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

        // The queue renders through IReportExporter, so a backend other than the Crystal
        // runtime can be plugged in: with a fake, no Crystal call happens and the result
        // carries the fake's bytes, extension and media type.
        [Test]
        public void ProcessDataExportsThroughTheInjectedBackend()
        {
            var exporter = new Mock<IReportExporter>();
            exporter.Setup(e => e.Export(It.IsAny<string>(), It.IsAny<Data>()))
                .Returns(new ReportExport(new byte[] { 9, 8, 7 }, "csv", "text/csv"));
            var analyzer = new Mock<IReportAnalyzer>(MockBehavior.Strict);
            var queue = new ExportQueue(_mockLogger.Object, ExportQueue.ReportsChannel, exporter.Object, analyzer.Object);
            string id = Guid.NewGuid().ToString();
            var item = new QueueItem { Id = id, ReportTemplate = new byte[] { 1, 2, 3 }, Data = new Data { ExportAs = ExportTypes.CSV } };

            var result = queue.ProcessData(item, null).GetAwaiter().GetResult();

            Assert.Multiple((Action)(() =>
            {
                Assert.That(result.FileContent, Is.EqualTo(new byte[] { 9, 8, 7 }));
                Assert.That(result.Format, Is.EqualTo("csv"));
                Assert.That(result.Metadata, Is.EqualTo("text/csv"));
                Assert.That(result.FileName, Is.EqualTo(id + ".csv"));
                Assert.That(Directory.Exists(WorkingDirFor(id)), Is.False);
            }));
            exporter.Verify(e => e.Export(It.Is<string>(p => p.EndsWith(id + ".rpt")), item.Data), Times.Once);
        }

        [Test]
        public void ProcessDataAnalyzesThroughTheInjectedBackend()
        {
            var exporter = new Mock<IReportExporter>(MockBehavior.Strict);
            var analyzer = new Mock<IReportAnalyzer>();
            analyzer.Setup(a => a.Analyze(It.IsAny<string>()))
                .Returns(new FullReportAnalysisResponse { Parameters = new System.Collections.Generic.List<string> { "Region" } });
            var queue = new ExportQueue(_mockLogger.Object, ExportQueue.AnalyzerChannel, exporter.Object, analyzer.Object);
            string id = Guid.NewGuid().ToString();
            var item = new QueueItem { Id = id, ReportTemplate = new byte[] { 1, 2, 3 }, Data = null };

            var result = queue.ProcessData(item, null).GetAwaiter().GetResult();

            Assert.Multiple((Action)(() =>
            {
                Assert.That(result.Format, Is.EqualTo("json"));
                Assert.That(System.Text.Encoding.UTF8.GetString(result.FileContent), Does.Contain("Region"));
                Assert.That(Directory.Exists(WorkingDirFor(id)), Is.False);
            }));
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
