namespace Majorsilence.CrystalCmd.WorkQueues.IntegrationTests;

[TestFixture]
[Category("Integration")]
[Category("SqlServer")]
public class WorkQueueSqlServerTests : WorkQueueTestBase
{
    protected override WorkQueue CreateQueue(string channel)
    {
        var sqlDefs = new WorkQueueSqlDefs(SqlType.SqlServer);
        return new WorkQueue(sqlDefs, SqlType.SqlServer,
            ContainerSetup.SqlServer.GetConnectionString(), channel);
    }
}
