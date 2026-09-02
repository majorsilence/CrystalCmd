namespace Majorsilence.CrystalCmd.WorkQueues.IntegrationTests;

[TestFixture]
[Category("Integration")]
[Category("SqlServer")]
public class WorkQueueSqlServerTests : WorkQueueTestBase
{
    [OneTimeSetUp]
    public void RequireContainer()
    {
        if (ContainerSetup.SqlServer is null)
        {
            Assert.Ignore($"SQL Server container is not available on this host: {ContainerSetup.SqlServerUnavailableReason}");
        }
    }

    protected override WorkQueue CreateQueue(string channel, int leaseMinutes)
    {
        var sqlDefs = new WorkQueueSqlDefs(SqlType.SqlServer);
        return new WorkQueue(sqlDefs, SqlType.SqlServer,
            ContainerSetup.SqlServer!.GetConnectionString(), channel, leaseMinutes);
    }
}
