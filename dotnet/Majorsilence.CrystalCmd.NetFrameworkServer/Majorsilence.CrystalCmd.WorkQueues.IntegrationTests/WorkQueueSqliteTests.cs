namespace Majorsilence.CrystalCmd.WorkQueues.IntegrationTests;

/// <summary>
/// SQLite is the default configuration shipped in appsettings.json, so it gets the
/// same test suite. No container needed - each fixture runs against its own file.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Sqlite")]
public class WorkQueueSqliteTests : WorkQueueTestBase
{
    private string _dbPath = string.Empty;

    [OneTimeSetUp]
    public void CreateDatabaseFile()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"crystalcmd-workqueue-tests-{Guid.NewGuid():N}.db");
    }

    [OneTimeTearDown]
    public void DeleteDatabaseFile()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    // The server and its workers are separate processes on this one file. Rollback-journal
    // mode made the server's poll and the worker's claim collide with "database is locked";
    // Migrate() switches the file to WAL, where readers and the writer never block each other.
    [Test]
    public async Task Migrate_PutsTheFileInWalMode()
    {
        await CreateQueue(Channel).Migrate();

        using var con = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath};");
        await con.OpenAsync();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var mode = (await cmd.ExecuteScalarAsync())?.ToString();

        Assert.That(mode, Is.EqualTo("wal").IgnoreCase);
    }

    protected override WorkQueue CreateQueue(string channel, int leaseMinutes)
    {
        var sqlDefs = new WorkQueueSqlDefs(SqlType.Sqlite);
        return new WorkQueue(sqlDefs, SqlType.Sqlite,
            $"Data Source={_dbPath};", channel, leaseMinutes);
    }
}
