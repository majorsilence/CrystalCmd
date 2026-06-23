import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Mirrors Majorsilence.CrystalCmd.Common.FullReportAnalysisResponse. Field names are
 * PascalCase to match the JSON the C# server emits, so a C# client can consume a Java
 * server's /analyzer output and vice versa.
 */
public class FullReportAnalysisResponse {
    public List<String> Parameters = new ArrayList<>();
    public Map<String, String> ParametersExtended = new LinkedHashMap<>();
    public List<DataTableAnalysisDto> DataTables = new ArrayList<>();
    public List<FullSubReportAnalysisDto> SubReports = new ArrayList<>();
    public List<ReportObjectsDto> ReportObjects = new ArrayList<>();

    public static class FullSubReportAnalysisDto {
        public String SubreportName;
        public List<String> Parameters = new ArrayList<>();
        public List<DataTableAnalysisDto> DataTables = new ArrayList<>();
    }

    public static class DataTableAnalysisDto {
        public String DataTableName;
        public List<String> ColumnNames = new ArrayList<>();
    }

    public static class ReportObjectsDto {
        public String ObjectName;
        public int Width;
        public int TopPosition;
        public String ObjectValue;
    }
}
