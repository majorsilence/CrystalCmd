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

    protected override WorkQueue CreateQueue(string channel, int leaseMinutes)
    {
        var sqlDefs = new WorkQueueSqlDefs(SqlType.Sqlite);
        return new WorkQueue(sqlDefs, SqlType.Sqlite,
            $"Data Source={_dbPath};", channel, leaseMinutes);
    }
}
