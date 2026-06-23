/**
 * Per-dialect SQL for the work queue, mirroring the C# WorkQueueSqlDefs. All statements use
 * positional parameters (?) and a consistent parameter order across dialects so WorkQueue
 * can bind them uniformly. Migration strings may contain multiple statements separated by
 * ';' — WorkQueue splits and executes them individually.
 */
public class WorkQueueSqlDefs {

    public final String enqueueSql;
    public final String dequeueSql;
    public final String dequeueByIdSql;
    public final String claimWorkItemSql;
    public final String updateFailureCountSql;
    public final String getSql;
    public final String generatedReportInsertSql;
    public final String markAsCompletedSql;
    public final String migrateWorkQueueSql;
    public final String migrateGeneratedReportsSql;
    public final String cleanupWorkQueueSql;
    public final String cleanupGeneratedReportsSql;

    public WorkQueueSqlDefs(SqlType sqlType) {
        // Shared statements (identical across dialects). Parameter order:
        // enqueue: id, timecreatedutc, retrycount, nextretryutc, maxretries, status,
        //          timeprocessedutc, lockid, lockeduntilutc, channel, payload, errormessage
        enqueueSql = "INSERT INTO workqueue (id, timecreatedutc, retrycount, nextretryutc, maxretries, status, "
                + "timeprocessedutc, lockid, lockeduntilutc, channel, payload, errormessage) "
                + "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
        dequeueByIdSql = "SELECT * FROM workqueue WHERE id = ?";
        claimWorkItemSql = "UPDATE workqueue SET status = ? WHERE id = ?";          // params: status, id
        updateFailureCountSql = "UPDATE workqueue SET retrycount = retrycount + 1, errormessage = ?, "
                + "status = ? WHERE id = ?";                                         // params: errormessage, pendingstatus, id
        getSql = "SELECT * FROM generatedreports WHERE id = ?";
        generatedReportInsertSql = "INSERT INTO generatedreports (id, format, generatedutc, filecontent, filename, metadata) "
                + "VALUES (?, ?, ?, ?, ?, ?)";
        markAsCompletedSql = "UPDATE workqueue SET status = ?, timeprocessedutc = ? WHERE id = ?"; // status, timeprocessedutc, id

        switch (sqlType) {
            case SQLSERVER:
                dequeueSql = "SELECT TOP 1 * FROM workqueue WITH (ROWLOCK, UPDLOCK, READPAST) "
                        + "WHERE status = ? AND retrycount <= maxretries AND channel = ? "
                        + "ORDER BY timecreatedutc ASC";                              // params: status, channel
                migrateWorkQueueSql =
                        "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'workqueue') "
                        + "CREATE TABLE workqueue ("
                        + "id NVARCHAR(128) NOT NULL PRIMARY KEY, timecreatedutc DATETIME2 NOT NULL, "
                        + "retrycount INT NOT NULL, nextretryutc DATETIME2 NULL, maxretries INT NOT NULL, "
                        + "status INT NOT NULL, timeprocessedutc DATETIME2 NULL, lockid NVARCHAR(50) NULL, "
                        + "lockeduntilutc DATETIME2 NULL, channel NVARCHAR(50) NOT NULL, payload NVARCHAR(MAX) NOT NULL, "
                        + "errormessage NVARCHAR(1000) NULL)";
                migrateGeneratedReportsSql =
                        "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'generatedreports') "
                        + "CREATE TABLE generatedreports ("
                        + "id NVARCHAR(128) NOT NULL PRIMARY KEY, format NVARCHAR(10) NOT NULL, "
                        + "generatedutc DATETIME2 NOT NULL, filecontent VARBINARY(MAX) NOT NULL, "
                        + "filename NVARCHAR(256) NOT NULL, metadata NVARCHAR(MAX) NULL)";
                cleanupWorkQueueSql = "DELETE FROM workqueue WHERE timeprocessedutc < DATEADD(minute, -30, GETUTCDATE()) "
                        + "AND status NOT IN (1, 2)";
                cleanupGeneratedReportsSql = "DELETE FROM generatedreports WHERE generatedutc < DATEADD(minute, -30, GETUTCDATE())";
                break;

            case POSTGRESQL:
                dequeueSql = "SELECT * FROM workqueue WHERE status = ? AND retrycount <= maxretries AND channel = ? "
                        + "ORDER BY timecreatedutc ASC LIMIT 1 FOR UPDATE SKIP LOCKED";
                migrateWorkQueueSql = "CREATE TABLE IF NOT EXISTS workqueue ("
                        + "id VARCHAR(128) PRIMARY KEY, timecreatedutc TIMESTAMP NOT NULL, retrycount INT NOT NULL, "
                        + "nextretryutc TIMESTAMP NULL, maxretries INT NOT NULL, status INT NOT NULL, "
                        + "timeprocessedutc TIMESTAMP NULL, lockid VARCHAR(50) NULL, lockeduntilutc TIMESTAMP NULL, "
                        + "channel VARCHAR(50) NOT NULL, payload TEXT NOT NULL, errormessage VARCHAR(1000) NULL);"
                        + "CREATE INDEX IF NOT EXISTS ix_workqueue_timecreatedutc ON workqueue (timecreatedutc)";
                migrateGeneratedReportsSql = "CREATE TABLE IF NOT EXISTS generatedreports ("
                        + "id VARCHAR(128) PRIMARY KEY, format VARCHAR(10) NOT NULL, generatedutc TIMESTAMP NOT NULL, "
                        + "filecontent BYTEA NOT NULL, filename VARCHAR(256) NOT NULL, metadata TEXT NULL)";
                cleanupWorkQueueSql = "DELETE FROM workqueue WHERE timeprocessedutc < (NOW() AT TIME ZONE 'UTC') - INTERVAL '30 minutes' "
                        + "AND status NOT IN (1, 2)";
                cleanupGeneratedReportsSql = "DELETE FROM generatedreports WHERE generatedutc < (NOW() AT TIME ZONE 'UTC') - INTERVAL '30 minutes'";
                break;

            case SQLITE:
                dequeueSql = "SELECT * FROM workqueue WHERE status = ? AND retrycount <= maxretries AND channel = ? "
                        + "ORDER BY timecreatedutc ASC LIMIT 1";
                migrateWorkQueueSql = "CREATE TABLE IF NOT EXISTS workqueue ("
                        + "id TEXT PRIMARY KEY, timecreatedutc TEXT NOT NULL, retrycount INTEGER NOT NULL, "
                        + "nextretryutc TEXT, maxretries INTEGER NOT NULL, status INTEGER NOT NULL, "
                        + "timeprocessedutc TEXT, lockid TEXT, lockeduntilutc TEXT, channel TEXT NOT NULL, "
                        + "payload TEXT NOT NULL, errormessage TEXT);"
                        + "CREATE INDEX IF NOT EXISTS ix_workqueue_timecreatedutc ON workqueue (timecreatedutc)";
                migrateGeneratedReportsSql = "CREATE TABLE IF NOT EXISTS generatedreports ("
                        + "id TEXT PRIMARY KEY, format TEXT NOT NULL, generatedutc TEXT NOT NULL, "
                        + "filecontent BLOB NOT NULL, filename TEXT NOT NULL, metadata TEXT)";
                cleanupWorkQueueSql = "DELETE FROM workqueue WHERE timeprocessedutc < datetime('now', '-30 minutes') "
                        + "AND status NOT IN (1, 2)";
                cleanupGeneratedReportsSql = "DELETE FROM generatedreports WHERE generatedutc < datetime('now', '-30 minutes')";
                break;

            case H2:
            default:
                dequeueSql = "SELECT * FROM workqueue WHERE status = ? AND retrycount <= maxretries AND channel = ? "
                        + "ORDER BY timecreatedutc ASC LIMIT 1 FOR UPDATE";
                migrateWorkQueueSql = "CREATE TABLE IF NOT EXISTS workqueue ("
                        + "id VARCHAR(128) PRIMARY KEY, timecreatedutc TIMESTAMP NOT NULL, retrycount INT NOT NULL, "
                        + "nextretryutc TIMESTAMP NULL, maxretries INT NOT NULL, status INT NOT NULL, "
                        + "timeprocessedutc TIMESTAMP NULL, lockid VARCHAR(50) NULL, lockeduntilutc TIMESTAMP NULL, "
                        + "channel VARCHAR(50) NOT NULL, payload CLOB NOT NULL, errormessage VARCHAR(1000) NULL);"
                        + "CREATE INDEX IF NOT EXISTS ix_workqueue_timecreatedutc ON workqueue (timecreatedutc)";
                migrateGeneratedReportsSql = "CREATE TABLE IF NOT EXISTS generatedreports ("
                        + "id VARCHAR(128) PRIMARY KEY, format VARCHAR(10) NOT NULL, generatedutc TIMESTAMP NOT NULL, "
                        + "filecontent BLOB NOT NULL, filename VARCHAR(256) NOT NULL, metadata CLOB NULL)";
                cleanupWorkQueueSql = "DELETE FROM workqueue WHERE timeprocessedutc < DATEADD('MINUTE', -30, CURRENT_TIMESTAMP()) "
                        + "AND status NOT IN (1, 2)";
                cleanupGeneratedReportsSql = "DELETE FROM generatedreports WHERE generatedutc < DATEADD('MINUTE', -30, CURRENT_TIMESTAMP())";
                break;
        }
    }
}
