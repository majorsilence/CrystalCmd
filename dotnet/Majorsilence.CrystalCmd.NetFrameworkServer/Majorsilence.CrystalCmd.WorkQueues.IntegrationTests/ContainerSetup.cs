using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Majorsilence.CrystalCmd.WorkQueues.IntegrationTests;

/// <summary>
/// Starts SQL Server and PostgreSQL containers once for the entire test assembly run.
/// Requires Docker to be running on the host.
/// </summary>
[SetUpFixture]
public class ContainerSetup
{
#pragma warning disable NUnit1032
    public static MsSqlContainer SqlServer { get; private set; } = null!;
    public static PostgreSqlContainer PostgreSql { get; private set; } = null!;
#pragma warning restore NUnit1032

    [OneTimeSetUp]
    public async Task StartContainers()
    {
        SqlServer = new MsSqlBuilder().Build();
        PostgreSql = new PostgreSqlBuilder().Build();
        await Task.WhenAll(SqlServer.StartAsync(), PostgreSql.StartAsync());
    }

    [OneTimeTearDown]
    public async Task StopContainers()
    {
        await Task.WhenAll(
            SqlServer.DisposeAsync().AsTask(),
            PostgreSql.DisposeAsync().AsTask());
    }
}
