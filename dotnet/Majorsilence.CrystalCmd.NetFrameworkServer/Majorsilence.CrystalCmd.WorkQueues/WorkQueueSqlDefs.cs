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
        private readonly string _cleanupWorkQueueSql;
        private readonly string _cleanupGeneratedReportsSql;

        public string EnqueueSql => _enqueueSql;
        public string DequeueSql => _dequeueSql;
        public string DequeueByIdSql => _dequeueByIdSql;
        public string UpdateFailureCountSql => _updateFailureCountSql;
        public string GetSql => _getSql;
        public string GeneratedReportInsertSql => _generatedReportInsertSql;
        public string MigrateWorkeQueueSql => _migrateWorkeQueueSql;
        public string MigrateGeneratedReportsSql => _migrateGeneratedReportsSql;
        public string MarkAsCompletedSql => _markAsCompletedSql;
        public string CleanupWorkQueueSql => _cleanupWorkQueueSql;
        public string CleanupGeneratedReportsSql => _cleanupGeneratedReportsSql;

        public WorkQueueSqlDefs(SqlType sqlType)
        {

            if (sqlType == SqlType.SqlServer)
            {
                _enqueueSql = @"INSERT INTO dbo.workqueue (id, timecreatedutc, retrycount, nextretryutc, maxretries, status, timeprocessedutc, lockid, lockeduntilutc, channel, payload, errormessage) 
                    VALUES(@p_id, @p_timecreatedutc, @p_retrycount, @p_nextretryutc, @p_maxretries, @p_status, @p_timeprocessedutc, @p_lockid, @p_lockeduntilutc, @p_channel, @p_payload, @p_errormessage);";
                // Claim in a single atomic statement: the row is selected and marked
                // Processing without a client-side transaction spanning round trips, so a
                // worker that dies mid-claim cannot leave locks held on the server.
                // Rows whose lease (lockeduntilutc) has expired are reclaimed here.
                _dequeueSql = @"
                    WITH claimed AS (
                        SELECT TOP 1 * FROM dbo.workqueue WITH (ROWLOCK, UPDLOCK, READPAST)
                        WHERE channel = @p_channel
                          AND (status = @p_pendingstatus
                               OR (status = @p_processingstatus AND lockeduntilutc < @p_now))
                          AND retrycount <= maxretries
                        ORDER BY timecreatedutc ASC
                    )
                    UPDATE claimed
                    SET status = @p_processingstatus,
                        lockid = @p_lockid,
                        lockeduntilutc = @p_lockeduntilutc
                    OUTPUT inserted.*;";
                _dequeueByIdSql = @"
                    SELECT TOP 1 * FROM dbo.workqueue
                    WHERE Id = @p_id;";
                _getSql = @"SELECT * FROM dbo.generatedreports WHERE id = @p_id";
                // Release the lease so the item is immediately retryable, and once the
                // retries are exhausted park it as Failed with a timeprocessedutc so the
                // cleanup job can eventually reap it instead of leaving it in the table
                // forever.
                _updateFailureCountSql = @"UPDATE dbo.workqueue
                    SET RetryCount = RetryCount + 1,
                        ErrorMessage = @p_errorMessage,
                        status = CASE WHEN RetryCount + 1 > MaxRetries
                                      THEN @p_failedstatus ELSE @p_pendingstatus END,
                        timeprocessedutc = CASE WHEN RetryCount + 1 > MaxRetries
                                      THEN @p_timeprocessedutc ELSE timeprocessedutc END,
                        lockid = NULL,
                        lockeduntilutc = NULL
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
                    IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = 'IX_workqueue_timecreatedutc' AND object_id = OBJECT_ID('dbo.workqueue'))
                    BEGIN
                        CREATE INDEX IX_workqueue_timecreatedutc ON dbo.workqueue (timecreatedutc);
                    END
                    -- Supports the dequeue seek. Without it the UPDLOCK scan takes a lock on
                    -- every row it examines, so one stuck worker blocks the whole table.
                    IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = 'IX_workqueue_dequeue' AND object_id = OBJECT_ID('dbo.workqueue'))
                    BEGIN
                        CREATE INDEX IX_workqueue_dequeue ON dbo.workqueue (channel, status, timecreatedutc)
                            INCLUDE (retrycount, maxretries, lockeduntilutc);
                    END
                    -- Supports the cleanup delete.
                    IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = 'IX_workqueue_timeprocessedutc' AND object_id = OBJECT_ID('dbo.workqueue'))
                    BEGIN
                        CREATE INDEX IX_workqueue_timeprocessedutc ON dbo.workqueue (timeprocessedutc) INCLUDE (status);
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
                    -- id is already the clustered primary key; the old index was redundant.
                    IF EXISTS (SELECT name FROM sys.indexes WHERE name = 'IX_generatedreports_id' AND object_id = OBJECT_ID('dbo.generatedreports'))
                    BEGIN
                        DROP INDEX IX_generatedreports_id ON dbo.generatedreports;
                    END
                    IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = 'IX_generatedreports_generatedutc' AND object_id = OBJECT_ID('dbo.generatedreports'))
                    BEGIN
                        CREATE INDEX IX_generatedreports_generatedutc ON dbo.generatedreports (generatedutc);
                    END";
                _markAsCompletedSql = @"UPDATE dbo.workqueue 
                    SET status = @p_status,
                        timeprocessedutc = @p_timeprocessedutc,
                        lockid = NULL,
                        lockeduntilutc = NULL
                    WHERE id = @p_id;";

                // delete completed or failed items older than a 30 minutes.
                // Deleted in small batches: a single unbounded delete escalates to a table
                // lock once it holds ~5000 row locks, at which point READPAST no longer
                // helps and the delete blocks every worker. LOCK_TIMEOUT makes a contended
                // pass give up cheaply and retry on the next cycle instead of hanging.
                _cleanupWorkQueueSql = @"SET LOCK_TIMEOUT 5000;
                    DECLARE @wq_cutoff DATETIME2 = DATEADD(minute, -30, GETUTCDATE());
                    WHILE 1 = 1
                    BEGIN
                        DELETE TOP (500) FROM dbo.workqueue WITH (READPAST)
                        WHERE timeprocessedutc < @wq_cutoff AND (status != 1 and status != 2);
                        IF @@ROWCOUNT < 500 BREAK;
                    END";
                _cleanupGeneratedReportsSql = @"SET LOCK_TIMEOUT 5000;
                    DECLARE @gr_cutoff DATETIME2 = DATEADD(minute, -30, GETUTCDATE());
                    WHILE 1 = 1
                    BEGIN
                        DELETE TOP (500) FROM dbo.generatedreports WITH (READPAST)
                        WHERE generatedutc < @gr_cutoff;
                        IF @@ROWCOUNT < 500 BREAK;
                    END";
            }
            else if (sqlType == SqlType.PostgreSQL)
            {
                _enqueueSql = @"INSERT INTO public.workqueue (id, timecreatedutc, retrycount, nextretryutc, maxretries, status, timeprocessedutc, lockid, lockeduntilutc, channel, payload, errormessage) 
                    VALUES(@p_id, @p_timecreatedutc, @p_retrycount, @p_nextretryutc, @p_maxretries, @p_status, @p_timeprocessedutc, @p_lockid, @p_lockeduntilutc, @p_channel, @p_payload, @p_errormessage);";
                // Single atomic claim; see the SQL Server comment above.
                _dequeueSql = @"
                    WITH claimed AS (
                        SELECT id FROM public.workqueue
                        WHERE channel = @p_channel
                          AND (status = @p_pendingstatus
                               OR (status = @p_processingstatus AND lockeduntilutc < @p_now))
                          AND retrycount <= maxretries
                        ORDER BY timecreatedutc ASC
                        LIMIT 1
                        FOR UPDATE SKIP LOCKED
                    )
                    UPDATE public.workqueue wq
                    SET status = @p_processingstatus,
                        lockid = @p_lockid,
                        lockeduntilutc = @p_lockeduntilutc
                    FROM claimed c
                    WHERE wq.id = c.id
                    RETURNING wq.*;";
                _dequeueByIdSql = @"
                    SELECT * FROM public.workqueue
                    WHERE id=@p_id;";
                _getSql = @"SELECT * FROM public.generatedreports WHERE id = @p_id;";
                _updateFailureCountSql = @"UPDATE public.workqueue
                    SET retrycount = retrycount + 1,
                        errormessage = @p_errorMessage,
                        status = CASE WHEN retrycount + 1 > maxretries
                                      THEN @p_failedstatus ELSE @p_pendingstatus END,
                        timeprocessedutc = CASE WHEN retrycount + 1 > maxretries
                                      THEN @p_timeprocessedutc ELSE timeprocessedutc END,
                        lockid = NULL,
                        lockeduntilutc = NULL
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
                        -- Supports the dequeue seek.
                        IF NOT EXISTS (
                            SELECT 1 FROM pg_class c
                            JOIN pg_namespace n ON n.oid = c.relnamespace
                            WHERE c.relname = 'ix_workqueue_dequeue' AND n.nspname = 'public'
                        ) THEN
                            CREATE INDEX ix_workqueue_dequeue ON public.workqueue (channel, status, timecreatedutc);
                        END IF;
                        -- Supports the cleanup delete.
                        IF NOT EXISTS (
                            SELECT 1 FROM pg_class c
                            JOIN pg_namespace n ON n.oid = c.relnamespace
                            WHERE c.relname = 'ix_workqueue_timeprocessedutc' AND n.nspname = 'public'
                        ) THEN
                            CREATE INDEX ix_workqueue_timeprocessedutc ON public.workqueue (timeprocessedutc);
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
                        -- id is already the primary key; the old index was redundant.
                        DROP INDEX IF EXISTS public.ix_generatedreports_id;
                        IF NOT EXISTS (
                            SELECT 1 FROM pg_class c
                            JOIN pg_namespace n ON n.oid = c.relnamespace
                            WHERE c.relname = 'ix_generatedreports_generatedutc' AND n.nspname = 'public'
                        ) THEN
                            CREATE INDEX ix_generatedreports_generatedutc ON public.generatedreports (generatedutc);
                        END IF;
                    END
                    $$;";
                _markAsCompletedSql = @"UPDATE public.workqueue
                    SET status = @p_status,
                        timeprocessedutc = @p_timeprocessedutc,
                        lockid = NULL,
                        lockeduntilutc = NULL
                    WHERE id = @p_id;";
                // Deleted in batches so a large backlog does not hold locks (and bloat the
                // WAL) in one long statement; lock_timeout makes a contended pass give up
                // and retry on the next cycle.
                _cleanupWorkQueueSql = @"DO $$
                    DECLARE deleted_count INT;
                    BEGIN
                        SET LOCAL lock_timeout = '5s';
                        LOOP
                            WITH locked_rows AS (
                                SELECT id 
                                FROM public.workqueue
                                WHERE timeprocessedutc < (NOW() AT TIME ZONE 'UTC') - INTERVAL '30 minutes'
                                  AND status NOT IN (1, 2)
                                ORDER BY timeprocessedutc
                                LIMIT 500
                                FOR UPDATE SKIP LOCKED
                            )
                            DELETE FROM public.workqueue wq
                            USING locked_rows lr
                            WHERE wq.id = lr.id;
                            GET DIAGNOSTICS deleted_count = ROW_COUNT;
                            EXIT WHEN deleted_count < 500;
                        END LOOP;
                    END
                    $$;";
                _cleanupGeneratedReportsSql = @"DO $$
                    DECLARE deleted_count INT;
                    BEGIN
                        SET LOCAL lock_timeout = '5s';
                        LOOP
                            WITH expired_reports AS (
                                SELECT id 
                                FROM public.generatedreports
                                WHERE generatedutc < (NOW() AT TIME ZONE 'UTC') - INTERVAL '30 minutes'
                                ORDER BY generatedutc
                                LIMIT 500
                                FOR UPDATE SKIP LOCKED -- Locks available rows, ignores those currently in use
                            )
                            DELETE FROM public.generatedreports gr
                            USING expired_reports er
                            WHERE gr.id = er.id;
                            GET DIAGNOSTICS deleted_count = ROW_COUNT;
                            EXIT WHEN deleted_count < 500;
                        END LOOP;
                    END
                    $$;";
            }
            else
            {
                // SQLite approach with lock columns
                var now = DateTime.UtcNow;
                var lockId = Guid.NewGuid().ToString();

                _enqueueSql = @"INSERT INTO WorkQueue (Id, TimeCreatedUtc, RetryCount, NextRetryUtc, MaxRetries, Status, TimeProcessedUtc, LockId, LockedUntilUtc, Channel, Payload, ErrorMessage) 
                    VALUES(@p_id, @p_timecreatedutc, @p_retrycount, @p_nextretryutc, @p_maxretries, @p_status, @p_timeprocessedutc, @p_lockid, @p_lockeduntilutc, @p_channel, @p_payload, @p_errormessage);";
                // Single atomic claim; see the SQL Server comment above.
                _dequeueSql = @"UPDATE WorkQueue
                    SET Status = @p_processingstatus,
                        LockId = @p_lockid,
                        LockedUntilUtc = @p_lockeduntilutc
                    WHERE Id = (
                        SELECT Id FROM WorkQueue
                        WHERE Channel = @p_channel
                          AND (Status = @p_pendingstatus
                               OR (Status = @p_processingstatus AND LockedUntilUtc IS NOT NULL AND LockedUntilUtc < @p_now))
                          AND RetryCount <= MaxRetries
                        ORDER BY TimeCreatedUtc ASC
                        LIMIT 1)
                    RETURNING *;";
                _dequeueByIdSql = @"SELECT * FROM WorkQueue
                    WHERE id=@p_id";
                _getSql = @"SELECT * FROM generatedreports WHERE Id = @p_id";
                _updateFailureCountSql = @"UPDATE workqueue
                    SET retrycount = retrycount + 1,
                        errormessage = @p_errorMessage,
                        status = CASE WHEN retrycount + 1 > maxretries
                                      THEN @p_failedstatus ELSE @p_pendingstatus END,
                        timeprocessedutc = CASE WHEN retrycount + 1 > maxretries
                                      THEN @p_timeprocessedutc ELSE timeprocessedutc END,
                        lockid = NULL,
                        lockeduntilutc = NULL
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
                    CREATE INDEX IF NOT EXISTS IX_WorkQueue_TimeCreatedUtc ON WorkQueue (TimeCreatedUtc);
                    CREATE INDEX IF NOT EXISTS IX_WorkQueue_Dequeue ON WorkQueue (Channel, Status, TimeCreatedUtc);
                    CREATE INDEX IF NOT EXISTS IX_WorkQueue_TimeProcessedUtc ON WorkQueue (TimeProcessedUtc);";
                _migrateGeneratedReportsSql = @"-- SQLite
                    CREATE TABLE IF NOT EXISTS generatedreports (
                        id TEXT PRIMARY KEY,
                        format TEXT NOT NULL,
                        generatedutc TEXT NOT NULL,
                        filecontent BLOB NOT NULL,
                        filename TEXT NOT NULL,
                        metadata TEXT
                    );
                    -- id is already the primary key; the old index was redundant.
                    DROP INDEX IF EXISTS IX_generatedreports_id;
                    CREATE INDEX IF NOT EXISTS IX_generatedreports_generatedutc ON generatedreports (generatedutc);";
                _markAsCompletedSql = @"UPDATE WorkQueue
                    SET Status = @p_status,
                        TimeProcessedUtc = @p_timeprocessedutc,
                        LockId = NULL,
                        LockedUntilUtc = NULL
                    WHERE Id = @p_id;";
                _cleanupWorkQueueSql = @"DELETE FROM WorkQueue
                    WHERE TimeProcessedUtc < datetime('now', '-30 minutes') AND (status != 1 and status != 2);";
                _cleanupGeneratedReportsSql = @"DELETE FROM generatedreports
                    WHERE generatedutc < datetime('now', '-30 minutes');";
            }

        }

        public static SqlType ParseSqlType(string sqlType)
        {
            if (string.Equals(sqlType, "mssql", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sqlType, "sql", StringComparison.OrdinalIgnoreCase))
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
