using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.WorkQueues
{
    public class WorkQueueSqlDefs
    {
        private readonly string _enqueueSql;
        private readonly string _dequeueSql;
        private readonly string _dequeueByIdSql;
        private readonly string _updateFailureCountSql;
        private readonly string _getSql;
        private readonly string _generatedReportInsertSql;
        private readonly string _migrateWorkeQueueSql;
        private readonly string _migrateGeneratedReportsSql;
        private readonly string _markAsCompletedSql;

        public string EnqueueSql => _enqueueSql;
        public string DequeueSql => _dequeueSql;
        public string DequeueByIdSql => _dequeueByIdSql;
        public string UpdateFailureCountSql => _updateFailureCountSql;
        public string GetSql => _getSql;
        public string GeneratedReportInsertSql => _generatedReportInsertSql;
        public string MigrateWorkeQueueSql => _migrateWorkeQueueSql;
        public string MigrateGeneratedReportsSql => _migrateGeneratedReportsSql;
        public string MarkAsCompletedSql => _markAsCompletedSql;

        public WorkQueueSqlDefs(SqlType sqlType)
        {

            if (sqlType == SqlType.SqlServer)
            {
                _enqueueSql = @"INSERT INTO dbo.workqueue (id, timecreatedutc, retrycount, nextretryutc, maxretries, status, timeprocessedutc, lockid, lockeduntilutc, channel, payload, errormessage) 
                    VALUES(@p_id, @p_timecreatedutc, @p_retrycount, @p_nextretryutc, @p_maxretries, @p_status, @p_timeprocessedutc, @p_lockid, @p_lockeduntilutc, @p_channel, @p_payload, @p_errormessage);";
                _dequeueSql = @"
                    SELECT TOP 1 * FROM dbo.workqueue WITH (ROWLOCK, UPDLOCK, READPAST)
                    WHERE status = @Status AND RetryCount <= MaxRetries
                    ORDER BY timecreatedutc ASC;";
                _dequeueByIdSql = @"
                    SELECT TOP 1 * FROM dbo.workqueue
                    WHERE Id = @p_id;";
                _getSql = @"SELECT * FROM dbo.generatedreports WHERE id = @p_id";
                _updateFailureCountSql = @"UPDATE dbo.workqueue 
                    SET RetryCount = RetryCount + 1, 
                        ErrorMessage = @p_errorMessage
                    WHERE Id = @p_id;";
                _generatedReportInsertSql = @"INSERT INTO dbo.generatedreports (id, format, generatedutc, filecontent, filename, metadata)
                    VALUES (@p_id, @p_format, @p_generatedutc, @p_filecontent, @p_filename, @p_metadata);";
                _migrateWorkeQueueSql = @"-- SQL Server
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'workqueue')
                    BEGIN
                        CREATE TABLE dbo.workqueue (
                            id NVARCHAR(128) NOT NULL PRIMARY KEY,
                            timecreatedutc DATETIME2 NOT NULL,
                            retrycount INT NOT NULL,
                            nextretryutc DATETIME2 NULL,
                            maxretries INT NOT NULL,
                            status INT NOT NULL,
                            timeprocessedutc DATETIME2 NULL,
                            lockid NVARCHAR(50) NULL,
                            lockeduntilutc DATETIME2 NULL,
                            channel NVARCHAR(50) NOT NULL,
                            payload NVARCHAR(MAX) NOT NULL,
                            errormessage NVARCHAR(1000) NULL
                        );
                    END
                    IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = 'IX_workqueue_timecreatedutc')
                    BEGIN
                        CREATE INDEX IX_workqueue_timecreatedutc ON dbo.workqueue (timecreatedutc);
                    END";
                _migrateGeneratedReportsSql = @"-- SQL Server
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'generatedreports')
                    BEGIN
                        CREATE TABLE dbo.generatedreports (
                            id NVARCHAR(128) NOT NULL PRIMARY KEY,
                            format NVARCHAR(10) NOT NULL,
                            generatedutc DATETIME2 NOT NULL,
                            filecontent VARBINARY(MAX) NOT NULL,
                            filename NVARCHAR(256) NOT NULL,
                            metadata NVARCHAR(MAX) NULL
                        );
                    END
                    IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = 'IX_generatedreports_id')
                    BEGIN
                        CREATE INDEX IX_generatedreports_id ON dbo.generatedreports (id);
                    END";
                _markAsCompletedSql = @"UPDATE dbo.workqueue 
                    SET status = @p_status,
                        timeprocessedutc = @p_timeprocessedutc
                    WHERE id = @p_id;";
            }
            else if (sqlType == SqlType.PostgreSQL)
            {
                _enqueueSql = @"INSERT INTO public.workqueue (id, timecreatedutc, retrycount, nextretryutc, maxretries, status, timeprocessedutc, lockid, lockeduntilutc, channel, payload, errormessage) 
                    VALUES(@p_id, @p_timecreatedutc, @p_retrycount, @p_nextretryutc, @p_maxretries, @p_status, @p_timeprocessedutc, @p_lockid, @p_lockeduntilutc, @p_channel, @p_payload, @p_errormessage);";
                _dequeueSql = @"
                    SELECT * FROM public.workqueue
                    WHERE status = @p_status AND retrycount <= maxretries
                    ORDER BY timecreatedutc ASC
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED;";
                _dequeueByIdSql = @"
                    SELECT * FROM public.workqueue
                    WHERE id=@p_id;";
                _getSql = @"SELECT * FROM public.generatedreports WHERE id = @p_id;";
                _updateFailureCountSql = @"UPDATE public.workqueue 
                    SET retrycount = retrycount + 1, 
                        errormessage = @p_errorMessage
                    WHERE id = @p_id;";
                _generatedReportInsertSql = @"INSERT INTO public.generatedreports (id, format, generatedutc, filecontent, filename, metadata)
                    VALUES (@p_id, @p_format, @p_generatedutc, @p_filecontent, @p_filename, @p_metadata);";
                _migrateWorkeQueueSql = @"-- PostgreSQL
                    DO $$
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.tables 
                            WHERE table_name = 'workqueue' AND table_schema = 'public'
                        ) THEN
                            CREATE TABLE public.workqueue (
                                id VARCHAR(128) PRIMARY KEY,
                                timecreatedutc TIMESTAMP NOT NULL,
                                retrycount INT NOT NULL,
                                nextretryutc TIMESTAMP NULL,
                                maxretries INT NOT NULL,
                                status INT NOT NULL,
                                timeprocessedutc TIMESTAMP NULL,
                                lockid VARCHAR(50) NULL,
                                lockeduntilutc TIMESTAMP NULL,
                                channel VARCHAR(50) NOT NULL,
                                payload TEXT NOT NULL,
                                errormessage VARCHAR(1000) NULL
                            );
                        END IF;
                        IF NOT EXISTS (
                            SELECT 1 FROM pg_class c    
                            JOIN pg_namespace n ON n.oid = c.relnamespace
                            WHERE c.relname = 'ix_workqueue_timecreatedutc' AND n.nspname = 'public'
                        ) THEN
                            CREATE INDEX ix_workqueue_timecreatedutc ON public.workqueue (timecreatedutc);
                        END IF;
                    END
                    $$;";
                _migrateGeneratedReportsSql = @"-- PostgreSQL
                    DO $$
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1 FROM information_schema.tables 
                            WHERE table_name = 'generatedreports' AND table_schema = 'public'
                        ) THEN
                            CREATE TABLE public.generatedreports (
                                id VARCHAR(128) PRIMARY KEY,
                                format VARCHAR(10) NOT NULL,
                                generatedutc TIMESTAMP NOT NULL,
                                filecontent BYTEA NOT NULL,
                                filename VARCHAR(256) NOT NULL,
                                metadata TEXT NULL
                            );
                        END IF;
                        IF NOT EXISTS (
                            SELECT 1 FROM pg_class c    
                            JOIN pg_namespace n ON n.oid = c.relnamespace
                            WHERE c.relname = 'ix_generatedreports_id' AND n.nspname = 'public'
                        ) THEN
                            CREATE INDEX ix_generatedreports_id ON public.generatedreports (id);
                        END IF;
                    END
                    $$;";
                _markAsCompletedSql = @"UPDATE public.workqueue
                    SET status = @p_status,
                        timeprocessedutc = @p_timeprocessedutc
                    WHERE id = @p_id;";
            }
            else
            {
                // SQLite approach with lock columns
                var now = DateTime.UtcNow;
                var lockId = Guid.NewGuid().ToString();

                _enqueueSql = @"INSERT INTO WorkQueue (Id, TimeCreatedUtc, RetryCount, NextRetryUtc, MaxRetries, Status, TimeProcessedUtc, LockId, LockedUntilUtc, Channel, Payload, ErrorMessage) 
                    VALUES(@p_id, @p_timecreatedutc, @p_retrycount, @p_nextretryutc, @p_maxretries, @p_status, @p_timeprocessedutc, @p_lockid, @p_lockeduntilutc, @p_channel, @p_payload, @p_errormessage);";
                _dequeueSql = @"SELECT * FROM WorkQueue
                    WHERE Status=@p_status AND (LockedUntilUtc IS NULL OR LockedUntilUtc < @p_now) AND RetryCount <= MaxRetries
                    ORDER BY TimeCreatedUtc ASC 
                    LIMIT 1";
                _dequeueByIdSql = @"SELECT * FROM WorkQueue
                    WHERE id=@p_id AND (LockedUntilUtc IS NULL OR LockedUntilUtc < @p_now)";
                _getSql = @"SELECT * FROM generatedreports WHERE Id = @p_id";
                _updateFailureCountSql = @"UPDATE workqueue 
                    SET retrycount = retrycount + 1, 
                        errormessage = @p_errorMessage
                    WHERE id = @p_id;";
                _generatedReportInsertSql = @"INSERT INTO generatedreports (id, format, generatedutc, filecontent, filename, metadata)
                    VALUES (@p_id, @p_format, @p_generatedutc, @p_filecontent, @p_filename, @p_metadata);";
                _migrateWorkeQueueSql = @"-- SQLite
                    CREATE TABLE IF NOT EXISTS WorkQueue (
                        Id TEXT PRIMARY KEY,
                        TimeCreatedUtc TEXT NOT NULL,
                        RetryCount INTEGER NOT NULL,
                        NextRetryUtc TEXT,
                        MaxRetries INTEGER NOT NULL,
                        Status INTEGER NOT NULL,
                        TimeProcessedUtc TEXT,
                        LockId TEXT,
                        LockedUntilUtc TEXT,
                        Channel TEXT NOT NULL,
                        Payload TEXT NOT NULL,
                        ErrorMessage TEXT
                    );
                    CREATE INDEX IF NOT EXISTS IX_WorkQueue_TimeCreatedUtc ON WorkQueue (TimeCreatedUtc);";
                _migrateGeneratedReportsSql = @"-- SQLite
                    CREATE TABLE IF NOT EXISTS generatedreports (
                        id TEXT PRIMARY KEY,
                        format TEXT NOT NULL,
                        generatedutc TEXT NOT NULL,
                        filecontent BLOB NOT NULL,
                        filename TEXT NOT NULL,
                        metadata TEXT
                    );
                    CREATE INDEX IF NOT EXISTS IX_generatedreports_id ON generatedreports (id);";
                _markAsCompletedSql = @"UPDATE WorkQueue
                    SET Status = @p_status,
                        TimeProcessedUtc = @p_timeprocessedutc
                    WHERE Id = @p_id;";
            }

        }

        public static SqlType ParseSqlType(string sqlType)
        {
            if (string.Equals(sqlType, "mssql", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sqlType, "mssql", StringComparison.OrdinalIgnoreCase))
            {
                return SqlType.SqlServer;
            }
            else if (string.Equals(sqlType, "postgre", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sqlType, "postgresql", StringComparison.OrdinalIgnoreCase)
               || string.Equals(sqlType, "psql", StringComparison.OrdinalIgnoreCase))
            {
                return SqlType.PostgreSQL;
            }
            return SqlType.Sqlite;
        }

    }
}
