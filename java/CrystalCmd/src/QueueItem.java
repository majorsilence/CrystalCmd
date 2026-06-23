import java.util.Base64;

/**
 * Mirrors Majorsilence.CrystalCmd.WorkQueues.QueueItem. Serialised to the work-queue
 * payload column as JSON (gson); the report template is carried as Base64 so the JSON
 * stays compact and text-safe.
 */
public class QueueItem {
    public String Id;
    public String ReportTemplate; // Base64-encoded .rpt bytes
    public Data Data;

    public byte[] templateBytes() {
        if (ReportTemplate == null || ReportTemplate.isEmpty()) {
            return new byte[0];
        }
        return Base64.getDecoder().decode(ReportTemplate);
    }

    public static QueueItem create(String id, byte[] template, Data data) {
        QueueItem q = new QueueItem();
        q.Id = id;
        q.ReportTemplate = template == null ? null : Base64.getEncoder().encodeToString(template);
        q.Data = data;
        return q;
    }
}
