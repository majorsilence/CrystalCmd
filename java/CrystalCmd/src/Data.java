import java.util.List;
import java.util.Map;

/**
 * Mirrors Majorsilence.CrystalCmd.Common.Data. Field names match the C# property
 * names so the same JSON payload deserialises on either implementation.
 */
public class Data {

    private Map<String, Object> Parameters;
    private List<MoveObjects> MoveObjectPosition;
    private Map<String, String> DataTables;
    private List<String> EmptyDataTables;
    private List<SubReports> SubReportDataTables;
    private List<SubReports> EmptySubReportDataTables;
    private List<SubReportParameters> SubReportParameters;
    private Map<String, Boolean> Suppress;
    private Map<String, Integer> Resize;
    private Map<String, String> FormulaFieldText;
    private Map<String, Boolean> CanGrow;
    private Map<String, String> SortByField;
    private Map<String, String> ObjectText;
    private ExportTypes ExportAs = ExportTypes.PDF;
    private String TraceId;
    private String RecordSelectionFormula;

    public Map<String, Object> getParameters() {
        return this.Parameters;
    }

    public void setParameters(Map<String, Object> parameters) {
        this.Parameters = parameters;
    }

    public List<MoveObjects> getMoveObjectPosition() {
        return this.MoveObjectPosition;
    }

    public void setMoveObjectPosition(List<MoveObjects> moveObjectPosition) {
        this.MoveObjectPosition = moveObjectPosition;
    }

    public Map<String, String> getDataTables() {
        return this.DataTables;
    }

    public void setDataTables(Map<String, String> dataTables) {
        this.DataTables = dataTables;
    }

    public List<String> getEmptyDataTables() {
        return this.EmptyDataTables;
    }

    public void setEmptyDataTables(List<String> emptyDataTables) {
        this.EmptyDataTables = emptyDataTables;
    }

    public List<SubReports> getSubReportDataTables() {
        return this.SubReportDataTables;
    }

    public void setSubReportDataTables(List<SubReports> dataTables) {
        this.SubReportDataTables = dataTables;
    }

    public List<SubReports> getEmptySubReportDataTables() {
        return this.EmptySubReportDataTables;
    }

    public void setEmptySubReportDataTables(List<SubReports> dataTables) {
        this.EmptySubReportDataTables = dataTables;
    }

    public List<SubReportParameters> getSubReportParameters() {
        return this.SubReportParameters;
    }

    public void setSubReportParameters(List<SubReportParameters> subReportParameters) {
        this.SubReportParameters = subReportParameters;
    }

    public Map<String, Boolean> getSuppress() {
        return this.Suppress;
    }

    public void setSuppress(Map<String, Boolean> suppress) {
        this.Suppress = suppress;
    }

    public Map<String, Integer> getResize() {
        return this.Resize;
    }

    public void setResize(Map<String, Integer> resize) {
        this.Resize = resize;
    }

    public Map<String, String> getFormulaFieldText() {
        return this.FormulaFieldText;
    }

    public void setFormulaFieldText(Map<String, String> formulaFieldText) {
        this.FormulaFieldText = formulaFieldText;
    }

    public Map<String, Boolean> getCanGrow() {
        return this.CanGrow;
    }

    public void setCanGrow(Map<String, Boolean> canGrow) {
        this.CanGrow = canGrow;
    }

    public Map<String, String> getSortByField() {
        return this.SortByField;
    }

    public void setSortByField(Map<String, String> sortByField) {
        this.SortByField = sortByField;
    }

    public Map<String, String> getObjectText() {
        return this.ObjectText;
    }

    public void setObjectText(Map<String, String> objectText) {
        this.ObjectText = objectText;
    }

    public ExportTypes getExportAs() {
        return this.ExportAs == null ? ExportTypes.PDF : this.ExportAs;
    }

    public void setExportAs(ExportTypes exportAs) {
        this.ExportAs = exportAs;
    }

    public String getTraceId() {
        return this.TraceId;
    }

    public void setTraceId(String traceId) {
        this.TraceId = traceId;
    }

    public String getRecordSelectionFormula() {
        return this.RecordSelectionFormula;
    }

    public void setRecordSelectionFormula(String recordSelectionFormula) {
        this.RecordSelectionFormula = recordSelectionFormula;
    }
}
