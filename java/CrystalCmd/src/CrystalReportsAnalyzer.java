import com.crystaldecisions.sdk.occa.report.application.ISubreportClientDocument;
import com.crystaldecisions.sdk.occa.report.application.OpenReportOptions;
import com.crystaldecisions.sdk.occa.report.application.ReportClientDocument;
import com.crystaldecisions.sdk.occa.report.data.IField;
import com.crystaldecisions.sdk.occa.report.data.IParameterField;
import com.crystaldecisions.sdk.occa.report.data.ITable;
import com.crystaldecisions.sdk.occa.report.definition.IArea;
import com.crystaldecisions.sdk.occa.report.definition.IReportObject;
import com.crystaldecisions.sdk.occa.report.definition.ISection;
import com.crystaldecisions.sdk.occa.report.definition.ITextObject;
import com.crystaldecisions.sdk.occa.report.lib.IStrings;

/**
 * Java counterpart of the C# CrystalReportsAnalyzer: introspects a report's parameters,
 * data tables, subreports and report objects and returns a FullReportAnalysisResponse.
 */
public class CrystalReportsAnalyzer {

    public FullReportAnalysisResponse getFullAnalysis(String reportPath) throws Exception {
        ReportClientDocument doc = new ReportClientDocument();
        doc.setReportAppServer(ReportClientDocument.inprocConnectionString);
        doc.open(reportPath, OpenReportOptions._openAsReadOnly);
        try {
            FullReportAnalysisResponse r = new FullReportAnalysisResponse();
            for (Object o : doc.getDataDefController().getDataDefinition().getParameterFields()) {
                IParameterField p = (IParameterField) o;
                if (isMainReportParameter(p)) {
                    r.Parameters.add(p.getName());
                    r.ParametersExtended.put(p.getName(), parameterType(p));
                }
            }
            r.DataTables = dataTables(doc.getDatabase().getTables());
            r.SubReports = subReports(doc);
            r.ReportObjects = reportObjects(doc);
            return r;
        } finally {
            try {
                doc.close();
            } catch (Exception ignored) {
            }
        }
    }

    private static boolean isMainReportParameter(IParameterField p) {
        String reportName = p.getReportName();
        return reportName == null || reportName.isEmpty();
    }

    private static String parameterType(IParameterField p) {
        try {
            return p.getType().toVariantTypeString();
        } catch (Exception e) {
            return "";
        }
    }

    private java.util.List<FullReportAnalysisResponse.DataTableAnalysisDto> dataTables(Iterable<ITable> tables) {
        java.util.List<FullReportAnalysisResponse.DataTableAnalysisDto> list = new java.util.ArrayList<>();
        for (ITable table : tables) {
            FullReportAnalysisResponse.DataTableAnalysisDto dto = new FullReportAnalysisResponse.DataTableAnalysisDto();
            dto.DataTableName = table.getName();
            for (IField f : table.getDataFields()) {
                dto.ColumnNames.add(f.getName());
            }
            list.add(dto);
        }
        return list;
    }

    private java.util.List<FullReportAnalysisResponse.FullSubReportAnalysisDto> subReports(ReportClientDocument doc)
            throws Exception {
        java.util.List<FullReportAnalysisResponse.FullSubReportAnalysisDto> list = new java.util.ArrayList<>();
        IStrings names = doc.getSubreportController().querySubreportNames();
        for (String name : names) {
            FullReportAnalysisResponse.FullSubReportAnalysisDto dto = new FullReportAnalysisResponse.FullSubReportAnalysisDto();
            dto.SubreportName = name;
            for (Object o : doc.getDataDefController().getDataDefinition().getParameterFields()) {
                IParameterField p = (IParameterField) o;
                if (name.equals(p.getReportName())) {
                    dto.Parameters.add(p.getName());
                }
            }
            try {
                ISubreportClientDocument sub = doc.getSubreportController().getSubreport(name);
                dto.DataTables = dataTables(sub.getDatabaseController().getDatabase().getTables());
            } catch (Exception ex) {
                System.out.println("Analyzer: subreport tables for " + name + " failed: " + ex.getMessage());
            }
            list.add(dto);
        }
        return list;
    }

    private java.util.List<FullReportAnalysisResponse.ReportObjectsDto> reportObjects(ReportClientDocument doc)
            throws Exception {
        java.util.List<FullReportAnalysisResponse.ReportObjectsDto> list = new java.util.ArrayList<>();
        for (IArea area : doc.getReportDefController().getReportDefinition().getAreas()) {
            for (ISection section : area.getSections()) {
                for (IReportObject obj : section.getReportObjects()) {
                    FullReportAnalysisResponse.ReportObjectsDto dto = new FullReportAnalysisResponse.ReportObjectsDto();
                    dto.ObjectName = obj.getName();
                    dto.Width = obj.getWidth();
                    dto.TopPosition = obj.getTop();
                    dto.ObjectValue = (obj instanceof ITextObject) ? ((ITextObject) obj).getText() : obj.toString();
                    list.add(dto);
                }
            }
        }
        return list;
    }
}
