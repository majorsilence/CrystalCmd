using Majorsilence.CrystalCmd.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace Majorsilence.CrystalCmd.WorkQueues.IntegrationTests;

/// <summary>
/// The processing loop itself, on SQLite so no container is needed.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Sqlite")]
public class QueueProcessorTests
{
    private sealed class FixedExporter : IReportExporter
    {
        public ReportExport Export(string reportPath, Data data) =>
            new ReportExport(new byte[] { 1, 2, 3 }, "pdf", "application/pdf");
    }

    private sealed class NoAnalyzer : IReportAnalyzer
    {
        public FullReportAnalysisResponse Analyze(string reportPath) => throw new NotSupportedException();
        public FullReportAnalysisResponse Analyze(byte[] reportTemplate) => throw new NotSupportedException();
    }

    // A loop's first migration can fail: several loops racing to create the tables on a
    // fresh PostgreSQL database, or a database that is not up yet. Here a file stands where
    // the database's folder belongs when the loop starts, so its first migration fails. The
    // loop used to end there, silently, leaving a worker that looked alive and processed
    // nothing.
    [Test]
    public async Task ALoopWhoseFirstMigrationFails_StillProcessesOnceTheDatabaseIsThere()
    {
        string root = Path.Combine(Path.GetTempPath(), $"crystalcmd-queueprocessor-{Guid.NewGuid():N}");
        string dbDir = Path.Combine(root, "db");
        string connection = $"Data Source={Path.Combine(dbDir, "queue.db")};";
        string channel = "reports";
        WorkQueue Queue() => new WorkQueue(new WorkQueueSqlDefs(SqlType.Sqlite), SqlType.Sqlite, connection, channel,
            WorkQueue.DefaultLeaseMinutes);

        var processor = new QueueProcessor(NullLogger.Instance, channel, new FixedExporter(), new NoAnalyzer(),
            Queue, Path.Combine(root, "work"));
        Directory.CreateDirectory(root);
        File.WriteAllText(dbDir, "not a folder");
        try
        {
            processor.Start();
            await Task.Delay(500);

            File.Delete(dbDir);
            var queue = Queue();
            await queue.Migrate();
            string id = Guid.NewGuid().ToString();
            await queue.Enqueue(new QueueItem { Id = id, ReportTemplate = new byte[] { 0 }, Data = new Data() });

            WorkItemStatus status = WorkItemStatus.Unknown;
            for (int i = 0; i < 100 && status != WorkItemStatus.Completed; i++)
            {
                await Task.Delay(100);
                (_, status, _) = await queue.Get(id);
            }

            Assert.That(status, Is.EqualTo(WorkItemStatus.Completed), "the loop did not survive its failed first migration");
        }
        finally
        {
            processor.Stop();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }
}
