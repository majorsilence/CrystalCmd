namespace Majorsilence.CrystalCmd.WorkQueues.IntegrationTests;

[TestFixture]
[Category("Integration")]
[Category("PostgreSql")]
public class WorkQueuePostgreSqlTests : WorkQueueTestBase
{
    protected override WorkQueue CreateQueue(string channel)
    {
        var sqlDefs = new WorkQueueSqlDefs(SqlType.PostgreSQL);
        return new WorkQueue(sqlDefs, SqlType.PostgreSQL,
            ContainerSetup.PostgreSql.GetConnectionString(), channel);
    }
}
