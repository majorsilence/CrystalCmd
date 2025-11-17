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
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
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
        private const string DefaultChannel = "crystal-reports";


        /// <summary>
        /// 
        /// </summary>
        /// <param name="enqueueSql">works with sql parameters @p_channel and @p_payload</param>
        /// <param name="dequeueSql">works with sql parameters @p_channel, @p_payload, and p_offset</param>
        /// <param name="getSql">works with sql parameter @p_id is used to get a report by id</param>
        public WorkQueue(WorkQueueSqlDefs sqlDefs,
            SqlType sqlType, string connectionString)
        {
            _sqlDefs = sqlDefs;
            _sqlType = sqlType;
            _connectionString = connectionString;
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

            return new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        }


        private static string GetSetting(string key)
        {
#if NET48
            var value = Environment.GetEnvironmentVariable($"appsettings__{key}");

            if (string.IsNullOrWhiteSpace(value))
            {
                value = ConfigurationManager.AppSettings[key];
            }

            return value;

#else
            IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
             return config.GetValue<string>(key);
#endif
        }

        public static WorkQueue CreateDefault()
        {
#if NET48
            var sqlTypeStr = GetSetting("WorkQueueSqlType");
            var connectionString = GetSetting("WorkQueueSqlConnection");
#else
            var sqlTypeStr = GetSetting("WorkQueue:SqlType");
            var connectionString = GetSetting("WorkQueue:SqlConnection");
#endif
            var sqlType = WorkQueueSqlDefs.ParseSqlType(sqlTypeStr);
            var sqlDefs = new WorkQueueSqlDefs(sqlType);

            return new WorkQueue(sqlDefs, sqlType, connectionString);
        }

        private async Task UpdateFailureCount(DbConnection con, string id, string errorMessage)
        {
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
                var lastAttemptUtcParam = command.CreateParameter();
                lastAttemptUtcParam.ParameterName = "@p_lastAttemptUtc";
                lastAttemptUtcParam.Value = DateTime.UtcNow;
                command.Parameters.Add(lastAttemptUtcParam);
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task Dequeue(Func<WorkQueuePoco, Task<GeneratedReportPoco>> callback)
        {
            using (var con = CreateConnection())
            {
                await con.OpenAsync();
                using (var txn = con.BeginTransaction())
                {
                    var result = await Dequeue<WorkQueuePoco>
                        (DefaultChannel, con, txn);
                    if (result != null)
                    {
                        try
                        {
                            var report = await callback(result);
                            await SaveGeneratedReport(report, con, txn);
                            // mark work item as completed
                            await MarkAsCompleted(con, txn, result.Id, WorkItemStatus.Completed);
                            txn.Commit();
                        }
                        catch (Exception ex)
                        {
                            txn.Rollback();
                            await UpdateFailureCount(con, result.Id, SafeSubString(ex.Message, 0, 1000));
                            throw;
                        }

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

                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                };

                if (generatedReportsPoco != null)
                {
                    return (generatedReportsPoco, WorkItemStatus.Completed);
                    //return (generatedReportsPoco.FileContent,
                    //    WorkItemStatus.Completed,
                    //    generatedReportsPoco.Format,
                    //    generatedReportsPoco.Format == "pdf" ? "application/pdf" : "application/octet-stream",
                    //    generatedReportsPoco.FileName);
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
                metadataParam.Value = report.Metadata;
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

        private async Task<T> Dequeue<T>(string channel, DbConnection con, DbTransaction txn, int offset = 0)
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentNullException(nameof(channel));

            if (con is null)
                throw new ArgumentNullException(nameof(con));

            if (txn is null)
                throw new ArgumentNullException(nameof(txn));

            if (con.State == ConnectionState.Closed) await con.OpenAsync();

            return await Query<T>(con, _sqlDefs.DequeueSql, new
            {
                p_channel = channel,
                p_status = (int)WorkItemStatus.Pending,
                p_now = DateTime.UtcNow
            }, txn);
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
    }
}
