import java.util.Map;

/**
 * Mirrors Majorsilence.CrystalCmd.Common.SubReportParameters.
 */
public class SubReportParameters {
    public String ReportName;
    public Map<String, Object> Parameters;

    public String getReportName() {
        return ReportName;
    }

    public void setReportName(String reportName) {
        this.ReportName = reportName;
    }

    public Map<String, Object> getParameters() {
        return Parameters;
    }

    public void setParameters(Map<String, Object> parameters) {
        this.Parameters = parameters;
    }
}
