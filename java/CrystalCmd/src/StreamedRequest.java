import java.util.Base64;

/**
 * Mirrors Majorsilence.CrystalCmd.Common.StreamedRequest, the body of a gzip request.
 * The C# client serialises Template (byte[]) as a Base64 string (Newtonsoft default),
 * so it is represented as a String here and decoded on demand.
 */
public class StreamedRequest {
    public Data ReportData;
    public String Template;

    public Data getReportData() {
        return ReportData;
    }

    public byte[] getTemplateBytes() {
        if (Template == null || Template.isEmpty()) {
            return new byte[0];
        }
        return Base64.getDecoder().decode(Template);
    }
}
