import com.google.gson.Gson;

import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.Statement;
import java.sql.Timestamp;
import java.sql.Types;
import java.time.Instant;

/**
 * Java port of Majorsilence.CrystalCmd.WorkQueues.WorkQueue. A minimal bespoke JDBC work
 * queue (no Hibernate/JPA dependency) supporting H2 (default), SQLite, PostgreSQL and SQL
 * Server. Enqueue from the HTTP layer; the worker calls dequeue() to claim, render, and
 * persist a result; clients fetch the result with get().
 */
public class WorkQueue {

    /** Callback that turns a claimed work item into a generated report. */
    public interface ReportProducer {
        GeneratedReport produce(QueueItem item) throws Exception;
    }

    /** Result of a get() lookup. */
    public static final class GetResult {
        public final GeneratedReport report;
        public final WorkItemStatus status;

        public GetResult(GeneratedReport report, WorkItemStatus status) {
            this.report = report;
            this.status = status;
        }
    }

    private static final Gson GSON = new Gson();

    private final SqlType sqlType;
    private final String connectionString;
    private final String channel;
    private final WorkQueueSqlDefs sql;

    public WorkQueue(SqlType sqlType, String connectionString, String channel) {
        this.sqlType = sqlType;
        this.connectionString = connectionString;
        this.channel = channel;
        this.sql = new WorkQueueSqlDefs(sqlType);
    }

    public static WorkQueue createDefault(String channel) {
        return new WorkQueue(SqlType.parse(Config.workQueueSqlType()), Config.workQueueConnection(), channel);
    }

    private Connection open() throws SQLException {
        return DriverManager.getConnection(connectionString);
    }

    public void migrate() throws SQLException {
        try (Connection con = open()) {
            execMultiple(con, sql.migrateWorkQueueSql);
            execMultiple(con, sql.migrateGeneratedReportsSql);
        }
    }

    private static void execMultiple(Connection con, String script) throws SQLException {
        for (String stmt : script.split(";")) {
            String s = stmt.trim();
            if (s.isEmpty()) {
                continue;
            }
            try (Statement st = con.createStatement()) {
                st.execute(s);
            }
        }
    }

    public void enqueue(QueueItem payload) throws SQLException {
        if (payload == null) {
            throw new IllegalArgumentException("payload");
        }
        String json = GSON.toJson(payload);
        try (Connection con = open();
             PreparedStatement ps = con.prepareStatement(sql.enqueueSql)) {
            ps.setString(1, payload.Id);
            ps.setTimestamp(2, Timestamp.from(Instant.now()));
            ps.setInt(3, 0);
            ps.setNull(4, Types.TIMESTAMP);
            ps.setInt(5, 2);
            ps.setInt(6, WorkItemStatus.Pending.code);
            ps.setNull(7, Types.TIMESTAMP);
            ps.setNull(8, Types.VARCHAR);
            ps.setNull(9, Types.TIMESTAMP);
            ps.setString(10, channel);
            ps.setString(11, json);
            ps.setNull(12, Types.VARCHAR);
            ps.executeUpdate();
        }
    }

    /**
     * Claim a single pending item (committing the claim immediately so no lock is held
     * during rendering), run the producer, then persist the result and mark it completed.
     * On producer failure the item is reset to Pending with an incremented retry count.
     */
    public void dequeue(ReportProducer producer) throws Exception {
        QueueItem claimed = null;
        String claimedId = null;

        try (Connection con = open()) {
            con.setAutoCommit(false);
            try {
                try (PreparedStatement ps = con.prepareStatement(sql.dequeueSql)) {
                    ps.setInt(1, WorkItemStatus.Pending.code);
                    ps.setString(2, channel);
                    try (ResultSet rs = ps.executeQuery()) {
                        if (rs.next()) {
                            claimedId = rs.getString("id");
                            String payload = rs.getString("payload");
                            claimed = GSON.fromJson(payload, QueueItem.class);
                        }
                    }
                }
                if (claimedId != null) {
                    try (PreparedStatement ps = con.prepareStatement(sql.claimWorkItemSql)) {
                        ps.setInt(1, WorkItemStatus.Processing.code);
                        ps.setString(2, claimedId);
                        ps.executeUpdate();
                    }
                }
                con.commit();
            } catch (SQLException ex) {
                con.rollback();
                throw ex;
            }
        }

        if (claimed == null) {
            return;
        }

        GeneratedReport report;
        try {
            report = producer.produce(claimed);
        } catch (Exception ex) {
            updateFailureCount(claimedId, safeMessage(ex.getMessage()));
            throw ex;
        }

        try (Connection con = open()) {
            con.setAutoCommit(false);
            try {
                saveGeneratedReport(con, report);
                markCompleted(con, claimedId, WorkItemStatus.Completed);
                con.commit();
            } catch (SQLException ex) {
                con.rollback();
                updateFailureCount(claimedId, safeMessage(ex.getMessage()));
                throw ex;
            }
        }
    }

    private void saveGeneratedReport(Connection con, GeneratedReport report) throws SQLException {
        try (PreparedStatement ps = con.prepareStatement(sql.generatedReportInsertSql)) {
            ps.setString(1, report.Id);
            ps.setString(2, report.Format);
            ps.setTimestamp(3, Timestamp.from(report.GeneratedUtc == null ? Instant.now() : report.GeneratedUtc));
            ps.setBytes(4, report.FileContent == null ? new byte[0] : report.FileContent);
            ps.setString(5, report.FileName);
            if (report.Metadata == null) {
                ps.setNull(6, Types.VARCHAR);
            } else {
                ps.setString(6, report.Metadata);
            }
            ps.executeUpdate();
        }
    }

    private void markCompleted(Connection con, String id, WorkItemStatus status) throws SQLException {
        try (PreparedStatement ps = con.prepareStatement(sql.markAsCompletedSql)) {
            ps.setInt(1, status.code);
            ps.setTimestamp(2, Timestamp.from(Instant.now()));
            ps.setString(3, id);
            ps.executeUpdate();
        }
    }

    private void updateFailureCount(String id, String errorMessage) {
        try (Connection con = open();
             PreparedStatement ps = con.prepareStatement(sql.updateFailureCountSql)) {
            ps.setString(1, errorMessage);
            ps.setInt(2, WorkItemStatus.Pending.code);
            ps.setString(3, id);
            ps.executeUpdate();
        } catch (SQLException ignored) {
            // best-effort failure bookkeeping
        }
    }

    public GetResult get(String id) throws SQLException {
        try (Connection con = open()) {
            try (PreparedStatement ps = con.prepareStatement(sql.getSql)) {
                ps.setString(1, id);
                try (ResultSet rs = ps.executeQuery()) {
                    if (rs.next()) {
                        GeneratedReport r = new GeneratedReport();
                        r.Id = rs.getString("id");
                        r.Format = rs.getString("format");
                        Timestamp ts = rs.getTimestamp("generatedutc");
                        r.GeneratedUtc = ts == null ? null : ts.toInstant();
                        r.FileContent = rs.getBytes("filecontent");
                        r.FileName = rs.getString("filename");
                        r.Metadata = rs.getString("metadata");
                        return new GetResult(r, WorkItemStatus.Completed);
                    }
                }
            }
            try (PreparedStatement ps = con.prepareStatement(sql.dequeueByIdSql)) {
                ps.setString(1, id);
                try (ResultSet rs = ps.executeQuery()) {
                    if (rs.next()) {
                        return new GetResult(null, WorkItemStatus.fromCode(rs.getInt("status")));
                    }
                }
            }
        }
        return new GetResult(null, WorkItemStatus.Unknown);
    }

    public void garbageCollection() throws SQLException {
        try (Connection con = open()) {
            execMultiple(con, sql.cleanupGeneratedReportsSql);
            execMultiple(con, sql.cleanupWorkQueueSql);
        }
    }

    private static String safeMessage(String message) {
        if (message == null) {
            return "";
        }
        // Redact connection-string secrets before this is persisted to the shared table.
        String redacted = message.replaceAll("(?i)\\b(password|pwd|user id|uid|data source|server|"
                + "initial catalog|database|accountkey|token)\\s*=\\s*[^;\"']+", "$1=***");
        return redacted.length() > 1000 ? redacted.substring(0, 1000) : redacted;
    }
}
