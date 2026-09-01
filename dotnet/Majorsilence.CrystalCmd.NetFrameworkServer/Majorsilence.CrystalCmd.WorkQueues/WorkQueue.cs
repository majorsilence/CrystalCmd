using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
#if NET48
using System.Configuration;
#else
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
#endif
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.WorkQueues
{
    /// <summary>
    /// The intent of this class is to provide a generic bespoke work queue interface to enqueue and dequeue work items.
    /// while keeping the dependencies minimal so it can be used in .NET Framework and .NET Core projects.
    /// An alternative approach would be MassTransit or Hangfire, but those are just another dependency to manage.
    /// This can be revisited to use a more standard solution in the future.
    /// 🤷
    /// </summary>
    public class WorkQueue
    {
        private readonly WorkQueueSqlDefs _sqlDefs;
        private readonly SqlType _sqlType;
        private readonly string _connectionString;
        private readonly string DefaultChannel;
        private readonly TimeSpan _leaseDuration;

        /// <summary>
        /// How long a claimed work item stays leased to the worker that claimed it.
        /// A worker that dies mid-report leaves its row in Processing; once the lease
        /// expires another worker reclaims it instead of the item being stranded
        /// forever. This must comfortably exceed the longest expected report
        /// generation time, otherwise a slow report can be picked up a second time.
        /// A negative value produces an already-expired lease, which is only useful
        /// for testing the reclaim path.
        /// </summary>
        public const int DefaultLeaseMinutes = 60;

        // Longer than the batched cleanup normally needs, but bounded: the SQL itself
        // sets a short lock timeout, so a contended pass fails fast rather than
        // sitting here.
        private const int CleanupCommandTimeoutSeconds = 120;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="enqueueSql">works with sql parameters @p_channel and @p_payload</param>
        /// <param name="dequeueSql">works with sql parameters @p_channel, @p_payload, and p_offset</param>
        /// <param name="getSql">works with sql parameter @p_id is used to get a report by id</param>

        public WorkQueue(WorkQueueSqlDefs sqlDefs,
            SqlType sqlType, string connectionString,
            string channel,
            int leaseMinutes = DefaultLeaseMinutes
            )
        {
            _sqlDefs = sqlDefs;
            _sqlType = sqlType;
            DefaultChannel = channel;
            _connectionString = ApplyApplicationName(connectionString, sqlType, channel);
            _leaseDuration = TimeSpan.FromMinutes(leaseMinutes);
        }

        /// <summary>
        /// Stamp the connection with an application name so these sessions are
        /// identifiable in the server's own diagnostics (sys.dm_exec_sessions,
        /// pg_stat_activity). Without it every session reports only the generic
        /// provider name and the cleanup service cannot be told apart from a worker.
        /// </summary>
        private static string ApplyApplicationName(string connectionString, SqlType sqlType, string channel)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }

            var applicationName = "CrystalCmd-" + (string.IsNullOrWhiteSpace(channel) ? "cleanup" : channel);

            try
            {
                if (sqlType == SqlType.SqlServer)
                {
                    var builder = new SqlConnectionStringBuilder(connectionString);
                    // The provider defaults this to its own name; only override that.
                    if (string.IsNullOrWhiteSpace(builder.ApplicationName)
                        || builder.ApplicationName.IndexOf("SqlClient Data Provider", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        builder.ApplicationName = applicationName;
                    }
                    return builder.ConnectionString;
                }

                if (sqlType == SqlType.PostgreSQL)
                {
                    var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
                    if (string.IsNullOrWhiteSpace(builder.ApplicationName))
                    {
                        builder.ApplicationName = applicationName;
                    }
                    return builder.ConnectionString;
                }
            }
            catch (Exception)
            {
                // A connection string we cannot parse is the connection's problem to
                // report, not this helper's; fall back to using it verbatim.
            }

            return connectionString;
        }

        private DbConnection CreateConnection()
        {
            if (_sqlType == SqlType.SqlServer)
            {
                return new SqlConnection(_connectionString);
            }
            else if (_sqlType == SqlType.PostgreSQL)
            {
                return new Npgsql.NpgsqlConnection(_connectionString);
            }

            EnsureSqliteDirectoryExists();
            return new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        }

        private void EnsureSqliteDirectoryExists()
        {
            var connectionStringBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(_connectionString);
            var dataSource = connectionStringBuilder.DataSource;

            if (string.IsNullOrWhiteSpace(dataSource) || string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var directoryPath = Path.GetDirectoryName(dataSource);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return;
            }

            Directory.CreateDirectory(directoryPath);
        }

        public static string GetSetting(string key
#if NET5_0_OR_GREATER
            , IConfiguration configuration
#endif
            )
        {
#if NET48
            var value = Environment.GetEnvironmentVariable($"appsettings__{key}");

            if (string.IsNullOrWhiteSpace(value))
            {
                value = ConfigurationManager.AppSettings[key];
            }

            return value;

#else
             return configuration.GetValue<string>(key);
#endif
        }

        public static WorkQueue CreateDefault(string channel
#if NET5_0_OR_GREATER
            , IConfiguration configuration
#endif
            )
        {
#if NET48
            var sqlTypeStr = GetSetting("WorkQueueSqlType");
            var connectionString = GetSetting("WorkQueueSqlConnection");
            var leaseMinutesStr = GetSetting("WorkQueueLeaseMinutes");
#else

            var sqlTypeStr = GetSetting("WorkQueue:SqlType", configuration);
            var connectionString = GetSetting("WorkQueue:SqlConnection", configuration);
            var leaseMinutesStr = GetSetting("WorkQueue:LeaseMinutes", configuration);
#endif
            var sqlType = WorkQueueSqlDefs.ParseSqlType(sqlTypeStr);
            var sqlDefs = new WorkQueueSqlDefs(sqlType);

            if (!int.TryParse(leaseMinutesStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var leaseMinutes)
                || leaseMinutes < 0)
            {
                leaseMinutes = DefaultLeaseMinutes;
            }

            return new WorkQueue(sqlDefs, sqlType, connectionString, channel, leaseMinutes);
        }

        private async Task UpdateFailureCount(string id, string errorMessage)
        {
            using (var con = CreateConnection())
            {
                await con.OpenAsync();
                using (var command = con.CreateCommand())
                {
                    command.CommandText = _sqlDefs.UpdateFailureCountSql;
                    command.CommandType = CommandType.Text;
                    var idParam = command.CreateParameter();
                    idParam.ParameterName = "@p_id";
                    idParam.Value = id;
                    command.Parameters.Add(idParam);
                    var errorMessageParam = command.CreateParameter();
                    errorMessageParam.ParameterName = "@p_errorMessage";
                    errorMessageParam.Value = errorMessage;
                    command.Parameters.Add(errorMessageParam);
                    var pendingStatusParam = command.CreateParameter();
                    pendingStatusParam.ParameterName = "@p_pendingstatus";
                    pendingStatusParam.Value = (int)WorkItemStatus.Pending;
                    command.Parameters.Add(pendingStatusParam);
                    var failedStatusParam = command.CreateParameter();
                    failedStatusParam.ParameterName = "@p_failedstatus";
                    failedStatusParam.Value = (int)WorkItemStatus.Failed;
                    command.Parameters.Add(failedStatusParam);
                    var timeProcessedUtcParam = command.CreateParameter();
                    timeProcessedUtcParam.ParameterName = "@p_timeprocessedutc";
                    timeProcessedUtcParam.Value = DateTime.UtcNow;
                    command.Parameters.Add(timeProcessedUtcParam);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task Dequeue(Func<WorkQueuePoco, Task<GeneratedReportPoco>> callback)
        {
            // Claim the item with a single atomic statement. No client-side transaction
            // spans round trips, so a worker killed mid-claim cannot leave a sleeping
            // session holding locks on the queue table - which previously blocked the
            // cleanup job until the orphaned connection was killed by hand.
            WorkQueuePoco result = await ClaimNextWorkItem();

            if (result == null) return;

            // Do the work outside any transaction — no locks held during report generation
            GeneratedReportPoco report;
            try
            {
                report = await callback(result);
            }
            catch (Exception ex)
            {
                await UpdateFailureCount(result.Id, SafeSubString(SanitizeErrorMessage(ex.Message), 0, 1000));
                throw;
            }

            // Transaction 2: persist the result and mark completed
            using (var con = CreateConnection())
            {
                await con.OpenAsync();
                using (var txn = con.BeginTransaction())
                {
                    try
                    {
                        await SaveGeneratedReport(report, con, txn);
                        await MarkAsCompleted(con, txn, result.Id, WorkItemStatus.Completed);
                        txn.Commit();
                    }
                    catch (Exception ex)
                    {
                        txn.Rollback();
                        await UpdateFailureCount(result.Id, SafeSubString(SanitizeErrorMessage(ex.Message), 0, 1000));
                        throw;
                    }
                }
            }
        }

        private async Task MarkAsCompleted(DbConnection con, DbTransaction txn, string id, WorkItemStatus status)
        {
            using (var command = con.CreateCommand())
            {
                command.CommandText = _sqlDefs.MarkAsCompletedSql;
                command.CommandType = CommandType.Text;
                command.Transaction = txn;
                var idParam = command.CreateParameter();
                idParam.ParameterName = "@p_id";
                idParam.Value = id;
                command.Parameters.Add(idParam);
                var timeProcessedUtcParam = command.CreateParameter();
                timeProcessedUtcParam.ParameterName = "@p_timeprocessedutc";
                timeProcessedUtcParam.Value = DateTime.UtcNow;
                command.Parameters.Add(timeProcessedUtcParam);
                var statusParam = command.CreateParameter();
                statusParam.ParameterName = "@p_status";
                statusParam.Value = (int)status;
                command.Parameters.Add(statusParam);
                await command.ExecuteNonQueryAsync();
            }
        }

        // Exception messages from the DB/Crystal layers frequently embed connection
        // strings. This row is stored in the shared work-queue table (and may be read by
        // other tenants/operators), so redact credential-bearing key=value pairs.
        private static readonly System.Text.RegularExpressions.Regex SecretKvRegex =
            new System.Text.RegularExpressions.Regex(
                @"(?i)\b(password|pwd|user id|uid|data source|server|address|initial catalog|database|account ?key|shared ?access ?key|token)\s*=\s*[^;""']+",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string SanitizeErrorMessage(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }
            return SecretKvRegex.Replace(input, m => m.Groups[1].Value + "=***");
        }

        private static string SafeSubString(string input, int startIndex, int length)
        {

            if (input.Length >= (startIndex + length))
            {
                return input.Substring(startIndex, length);
            }
            else
            {
                if (input.Length > startIndex)
                {
                    return input.Substring(startIndex);
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        // mini ORM query method, maps first row of result to T
        // This is a very basic implementation and does not handle complex types or relationships
        // It assumes that the column names in the result set match the property names in T
        // It also assumes that T has a parameterless constructor
        // This is to keep the dependencies minimal and avoid using Dapper or Entity Framework
        private static async Task<T> Query<T>(DbConnection connection, string sql, object param = null,
           DbTransaction transaction = null, int? commandTimeout = null,
           CommandType commandType = CommandType.Text)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.Connection = connection;
                cmd.CommandType = commandType;
                cmd.CommandText = sql;
                cmd.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    cmd.CommandTimeout = commandTimeout.Value;
                }

                if (param != null)
                {
                    var props = param.GetType().GetProperties();
                    foreach (var prop in props)
                    {
                        var value = prop.GetValue(param, null);
                        var parameter = cmd.CreateParameter();
                        parameter.ParameterName = $"@{prop.Name}";
                        parameter.Value = value;
                        cmd.Parameters.Add(parameter);
                    }
                }

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var dt = new DataTable();
                    dt.Load(reader);

                    if (dt.Rows.Count == 0)
                    {
                        return default(T);
                    }

                    // use reflection to map datatable to T
                    var obj = Activator.CreateInstance<T>();
                    var objType = typeof(T);
                    foreach (DataRow row in dt.Rows)
                    {
                        foreach (DataColumn column in dt.Columns)
                        {
                            var prop = objType.GetProperty(column.ColumnName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                            if (prop != null && row[column] != DBNull.Value)
                            {
                                object raw = row[column];
                                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                                object converted;
                                if (targetType.IsEnum)
                                {
                                    // handle nullable enums and numeric/string enum representations
                                    converted = Enum.ToObject(targetType, raw);
                                }
                                else
                                {
                                    converted = Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
                                }

                                prop.SetValue(obj, converted, null);
                            }
                        }
                    }
                    return obj;
                }
            }
        }


        public async Task<(GeneratedReportPoco Report, WorkItemStatus Status)>
            Get(string id)
        {
            using (var con = CreateConnection())
            {
                await con.OpenAsync();

                var generatedReportsPoco = await Query<GeneratedReportPoco>(con, _sqlDefs.GetSql, param: new { p_id = id });
                var workQueuePoco = await Query<WorkQueuePoco>(con, _sqlDefs.DequeueByIdSql, param: new { p_id = id, p_now = DateTime.UtcNow });

                if (generatedReportsPoco != null)
                {
                    return (generatedReportsPoco, WorkItemStatus.Completed);
                }
                else if (workQueuePoco != null)
                {
                    return (null, workQueuePoco.Status);
                }

                return (null, WorkItemStatus.Unknown);
            }
        }

        private async Task SaveGeneratedReport(GeneratedReportPoco report, DbConnection con, DbTransaction txn)
        {
            using (var command = con.CreateCommand())
            {
                command.CommandText = _sqlDefs.GeneratedReportInsertSql;
                command.CommandType = CommandType.Text;
                command.Transaction = txn;
                var idParam = command.CreateParameter();
                idParam.ParameterName = "@p_id";
                idParam.Value = report.Id;
                command.Parameters.Add(idParam);
                var formatParam = command.CreateParameter();
                formatParam.ParameterName = "@p_format";
                formatParam.Value = report.Format;
                command.Parameters.Add(formatParam);
                var generatedUtcParam = command.CreateParameter();
                generatedUtcParam.ParameterName = "@p_generatedutc";
                generatedUtcParam.Value = report.GeneratedUtc;
                command.Parameters.Add(generatedUtcParam);
                var fileContentParam = command.CreateParameter();
                fileContentParam.ParameterName = "@p_filecontent";
                fileContentParam.Value = report.FileContent;
                command.Parameters.Add(fileContentParam);
                var filenameParam = command.CreateParameter();
                filenameParam.ParameterName = "@p_filename";
                filenameParam.Value = report.FileName;
                command.Parameters.Add(filenameParam);
                var metadataParam = command.CreateParameter();
                metadataParam.ParameterName = "@p_metadata";
                metadataParam.Value = (object?)report.Metadata ?? DBNull.Value;
                command.Parameters.Add(metadataParam);
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<bool> Enqueue(QueueItem payload)
        {
            using (var con = CreateConnection())
            {
                await con.OpenAsync();

                if (payload == null) throw new ArgumentNullException(nameof(payload));


                string jsonPayload = JsonConvert.SerializeObject(payload, Formatting.Indented, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });

                if (con.State == ConnectionState.Closed)
                    await con.OpenAsync();

                using (var command = con.CreateCommand())
                {
                    command.CommandText = _sqlDefs.EnqueueSql;
                    command.CommandType = CommandType.Text;

                    var idParam = command.CreateParameter();
                    idParam.ParameterName = "@p_id";
                    idParam.Value = payload.Id;
                    command.Parameters.Add(idParam);

                    var timeCreatedUtcParam = command.CreateParameter();
                    timeCreatedUtcParam.ParameterName = "@p_timecreatedutc";
                    timeCreatedUtcParam.Value = DateTime.UtcNow;
                    command.Parameters.Add(timeCreatedUtcParam);

                    var retryCountParam = command.CreateParameter();
                    retryCountParam.ParameterName = "@p_retrycount";
                    retryCountParam.Value = 0;
                    command.Parameters.Add(retryCountParam);

                    var nextRetryUtcParam = command.CreateParameter();
                    nextRetryUtcParam.ParameterName = "@p_nextretryutc";
                    nextRetryUtcParam.Value = DBNull.Value;
                    command.Parameters.Add(nextRetryUtcParam);

                    var maxRetriesParam = command.CreateParameter();
                    maxRetriesParam.ParameterName = "@p_maxretries";
                    maxRetriesParam.Value = 2;
                    command.Parameters.Add(maxRetriesParam);

                    var statusParam = command.CreateParameter();
                    statusParam.ParameterName = "@p_status";
                    statusParam.Value = (int)WorkItemStatus.Pending;
                    command.Parameters.Add(statusParam);

                    var timeProcessedUtcParam = command.CreateParameter();
                    timeProcessedUtcParam.ParameterName = "@p_timeprocessedutc";
                    timeProcessedUtcParam.Value = DBNull.Value;
                    command.Parameters.Add(timeProcessedUtcParam);

                    var lockIdParam = command.CreateParameter();
                    lockIdParam.ParameterName = "@p_lockid";
                    lockIdParam.Value = DBNull.Value;
                    command.Parameters.Add(lockIdParam);

                    var lockedUntilUtcParam = command.CreateParameter();
                    lockedUntilUtcParam.ParameterName = "@p_lockeduntilutc";
                    lockedUntilUtcParam.Value = DBNull.Value;
                    command.Parameters.Add(lockedUntilUtcParam);

                    var channelParam = command.CreateParameter();
                    channelParam.ParameterName = "@p_channel";
                    channelParam.Value = DefaultChannel;
                    command.Parameters.Add(channelParam);

                    var payloadParam = command.CreateParameter();
                    payloadParam.ParameterName = "@p_payload";
                    payloadParam.Value = jsonPayload;
                    command.Parameters.Add(payloadParam);

                    var errorMessageParam = command.CreateParameter();
                    errorMessageParam.ParameterName = "@p_errormessage";
                    errorMessageParam.Value = DBNull.Value;
                    command.Parameters.Add(errorMessageParam);

                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    return rowsAffected > 0;
                }
            }
        }

        /// <summary>
        /// Selects the next pending item (or one whose lease has expired) and marks it
        /// Processing in one statement, returning the claimed row.
        /// </summary>
        private async Task<WorkQueuePoco> ClaimNextWorkItem()
        {
            if (string.IsNullOrWhiteSpace(DefaultChannel))
                throw new ArgumentNullException(nameof(DefaultChannel));

            var now = DateTime.UtcNow;

            using (var con = CreateConnection())
            {
                await con.OpenAsync();

                return await Query<WorkQueuePoco>(con, _sqlDefs.DequeueSql, new
                {
                    p_channel = DefaultChannel,
                    p_pendingstatus = (int)WorkItemStatus.Pending,
                    p_processingstatus = (int)WorkItemStatus.Processing,
                    p_now = now,
                    p_lockid = Guid.NewGuid().ToString(),
                    p_lockeduntilutc = now.Add(_leaseDuration)
                });
            }
        }

        public async Task Migrate()
        {
            using (var con = CreateConnection())
            {
                await con.OpenAsync();
                using (var command = con.CreateCommand())
                {
                    command.CommandText = _sqlDefs.MigrateWorkeQueueSql;
                    command.CommandType = CommandType.Text;
                    await command.ExecuteNonQueryAsync();

                    command.CommandText = _sqlDefs.MigrateGeneratedReportsSql;
                    command.CommandType = CommandType.Text;
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task GarbageCollection(CancellationToken cancellationToken = default)
        {
            using (var con = CreateConnection())
            {
                await con.OpenAsync(cancellationToken);
                using (var command = con.CreateCommand())
                {
                    command.CommandTimeout = CleanupCommandTimeoutSeconds;
                    command.CommandText = _sqlDefs.CleanupGeneratedReportsSql;
                    command.CommandType = CommandType.Text;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                    command.CommandText = _sqlDefs.CleanupWorkQueueSql;
                    command.CommandType = CommandType.Text;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }
    }
}
