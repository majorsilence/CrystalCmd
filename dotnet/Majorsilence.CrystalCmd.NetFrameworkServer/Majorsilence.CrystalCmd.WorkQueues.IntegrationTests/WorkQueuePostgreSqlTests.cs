namespace Majorsilence.CrystalCmd.WorkQueues.IntegrationTests;

[TestFixture]
[Category("Integration")]
[Category("PostgreSql")]
public class WorkQueuePostgreSqlTests : WorkQueueTestBase
{
    [OneTimeSetUp]
    public void RequireContainer()
    {
        if (ContainerSetup.PostgreSql is null)
        {
            Assert.Ignore($"PostgreSQL container is not available on this host: {ContainerSetup.PostgreSqlUnavailableReason}");
        }
    }

    protected override WorkQueue CreateQueue(string channel, int leaseMinutes)
    {
        var sqlDefs = new WorkQueueSqlDefs(SqlType.PostgreSQL);
        return new WorkQueue(sqlDefs, SqlType.PostgreSQL,
            ContainerSetup.PostgreSql!.GetConnectionString(), channel, leaseMinutes);
    }
}
