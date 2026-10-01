using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Majorsilence.CrystalCmd.WorkQueues.IntegrationTests;

/// <summary>
/// Starts SQL Server and PostgreSQL containers once for the entire test assembly run.
/// Startup is best-effort: on hosts that cannot run Linux containers (e.g. Windows CI
/// runners) the database-specific fixtures skip themselves via <see cref="Assert.Ignore(string)"/>
/// instead of failing every test in the assembly, and the SQLite tests still run.
/// </summary>
[SetUpFixture]
public class ContainerSetup
{
#pragma warning disable NUnit1032
    public static MsSqlContainer? SqlServer { get; private set; }
    public static PostgreSqlContainer? PostgreSql { get; private set; }
#pragma warning restore NUnit1032

    public static string? SqlServerUnavailableReason { get; private set; }
    public static string? PostgreSqlUnavailableReason { get; private set; }

    [OneTimeSetUp]
    public async Task StartContainers()
    {
        var sqlServerStart = TryStart(() => new MsSqlBuilder().Build());
        var postgreSqlStart = TryStart(() => new PostgreSqlBuilder().Build());

        (SqlServer, SqlServerUnavailableReason) = await sqlServerStart;
        (PostgreSql, PostgreSqlUnavailableReason) = await postgreSqlStart;
    }

    [OneTimeTearDown]
    public async Task StopContainers()
    {
        var pending = new List<Task>();
        if (SqlServer is not null)
        {
            pending.Add(SqlServer.DisposeAsync().AsTask());
        }

        if (PostgreSql is not null)
        {
            pending.Add(PostgreSql.DisposeAsync().AsTask());
        }

        await Task.WhenAll(pending);
    }

    private static async Task<(TContainer? Container, string? UnavailableReason)> TryStart<TContainer>(
        Func<TContainer> build)
        where TContainer : DotNet.Testcontainers.Containers.IContainer
    {
        TContainer container;
        try
        {
            // Build() already throws when no Docker endpoint is reachable at all.
            container = build();
        }
        catch (Exception ex)
        {
            return (default, ex.Message);
        }

        try
        {
            await container.StartAsync();
            return (container, null);
        }
        catch (Exception ex)
        {
            await container.DisposeAsync();
            return (default, ex.Message);
        }
    }
}
