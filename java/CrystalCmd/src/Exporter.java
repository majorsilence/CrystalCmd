import com.crystaldecisions.sdk.occa.report.application.ISubreportClientDocument;
import com.crystaldecisions.sdk.occa.report.application.OpenReportOptions;
import com.crystaldecisions.sdk.occa.report.application.ParameterFieldController;
import com.crystaldecisions.sdk.occa.report.application.ReportClientDocument;
import com.crystaldecisions.sdk.occa.report.data.Connections;
import com.crystaldecisions.sdk.occa.report.data.IFormulaField;
import com.crystaldecisions.sdk.occa.report.data.ParameterFieldType;
import com.crystaldecisions.sdk.occa.report.definition.IObjectFormat;
import com.crystaldecisions.sdk.occa.report.definition.IReportObject;
import com.crystaldecisions.sdk.occa.report.exportoptions.ReportExportFormat;
import com.crystaldecisions.sdk.occa.report.lib.IStrings;

import java.io.InputStream;
import java.sql.ResultSet;
import java.text.SimpleDateFormat;
import java.time.LocalDateTime;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;
import java.time.format.DateTimeFormatterBuilder;
import java.time.format.DateTimeParseException;
import java.util.Date;
import java.util.Map;

/**
 * Full-feature exporter, the Java counterpart of the C# Exporter + CrystalDocumentWrapper.
 * Renders any supported export format and applies the full set of Data binding features.
 *
 * NOTE: a handful of binding features depend on Java RAS SDK APIs that differ materially
 * from the .NET engine. Those (sort fields, object-text replacement) are applied
 * best-effort and logged if the SDK rejects them; everything else mirrors the C# server.
 */
public class Exporter {

    public static final class ExportResult {
        public final byte[] content;
        public final String fileExt;
        public final String mimeType;

        public ExportResult(byte[] content, String fileExt, String mimeType) {
            this.content = content;
            this.fileExt = fileExt;
            this.mimeType = mimeType;
        }
    }

    public ExportResult export(String reportPath, Data datafile) throws Exception {
        ReportClientDocument doc = new ReportClientDocument();
        doc.setReportAppServer(ReportClientDocument.inprocConnectionString);
        doc.open(reportPath, OpenReportOptions._openAsReadOnly);
        try {
            safeRemoveSubReportConnections(doc);
            safeRemoveMainConnections(doc);

            if (datafile != null) {
                applyData(doc, datafile);
            }

            ReportExportFormat fmt = exportFormat(datafile);
            String[] extMime = extAndMime(datafile);
            InputStream in = doc.getPrintOutputController().export(fmt);
            byte[] bytes = in.readAllBytes();
            return new ExportResult(bytes, extMime[0], extMime[1]);
        } finally {
            try {
                doc.close();
            } catch (Exception ignored) {
            }
        }
    }

    private ReportExportFormat exportFormat(Data datafile) {
        ExportTypes type = datafile == null ? ExportTypes.PDF : datafile.getExportAs();
        switch (type) {
            case CSV:
                return ReportExportFormat.characterSeparatedValues;
            case CrystalReport:
                return ReportExportFormat.crystalReports;
            case Excel:
                return ReportExportFormat.MSExcel;
            case ExcelDataOnly:
                return ReportExportFormat.recordToMSExcel;
            case RichText:
                return ReportExportFormat.RTF;
            case TEXT:
                return ReportExportFormat.text;
            case WordDoc:
                return ReportExportFormat.MSWord;
            case PDF:
            default:
                return ReportExportFormat.PDF;
        }
    }

    /** @return {fileExt, mimeType} */
    private String[] extAndMime(Data datafile) {
        ExportTypes type = datafile == null ? ExportTypes.PDF : datafile.getExportAs();
        switch (type) {
            case CSV:
                return new String[]{"csv", "text/csv"};
            case CrystalReport:
                return new String[]{"rpt", "application/octet-stream"};
            case Excel:
            case ExcelDataOnly:
                return new String[]{"xls", "application/vnd.ms-excel"};
            case RichText:
                return new String[]{"rtf", "application/rtf"};
            case TEXT:
                return new String[]{"txt", "text/plain"};
            case WordDoc:
                return new String[]{"doc", "application/msword"};
            case PDF:
            default:
                return new String[]{"pdf", "application/pdf"};
        }
    }

    private void applyData(ReportClientDocument doc, Data datafile) throws Exception {
        SimpleDateFormat fmt = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss");

        // Record selection formula (mirrors C# RecordSelectionFormula).
        if (notBlank(datafile.getRecordSelectionFormula())) {
            try {
                doc.getDataDefController().getRecordFilterController()
                        .setFormulaText(datafile.getRecordSelectionFormula());
            } catch (Exception ex) {
                log("record selection formula", ex);
            }
        }

        // Main report data tables.
        if (datafile.getDataTables() != null) {
            for (Map.Entry<String, String> item : datafile.getDataTables().entrySet()) {
                try {
                    ResultSet rs = new CsharpResultSet().Execute(item.getValue());
                    doc.getDatabaseController().setDataSource(rs, item.getKey(), item.getKey());
                } catch (Exception ex) {
                    log("data table " + item.getKey(), ex);
                }
            }
        }

        // Subreport data tables.
        if (datafile.getSubReportDataTables() != null) {
            for (SubReports item : datafile.getSubReportDataTables()) {
                try {
                    ResultSet rs = new CsharpResultSet().Execute(item.DataTable);
                    ISubreportClientDocument sub = doc.getSubreportController().getSubreport(item.ReportName);
                    sub.getDatabaseController().setDataSource(rs, item.TableName, item.TableName);
                } catch (Exception ex) {
                    log("subreport table " + item.ReportName, ex);
                }
            }
        }

        // Parameters.
        if (datafile.getParameters() != null) {
            for (Map.Entry<String, Object> item : datafile.getParameters().entrySet()) {
                try {
                    setParameterValue(doc, "", item.getKey(), item.getValue());
                } catch (Exception ex) {
                    log("parameter " + item.getKey(), ex);
                }
            }
        }

        // Subreport parameters.
        if (datafile.getSubReportParameters() != null) {
            for (SubReportParameters sub : datafile.getSubReportParameters()) {
                if (sub.Parameters == null) {
                    continue;
                }
                for (Map.Entry<String, Object> p : sub.Parameters.entrySet()) {
                    try {
                        setParameterValue(doc, sub.ReportName, p.getKey(), p.getValue());
                    } catch (Exception ex) {
                        log("subreport parameter " + sub.ReportName + "." + p.getKey(), ex);
                    }
                }
            }
        }

        // Formula field text.
        if (datafile.getFormulaFieldText() != null) {
            for (Map.Entry<String, String> item : datafile.getFormulaFieldText().entrySet()) {
                try {
                    setFormulaText(doc, item.getKey(), item.getValue());
                } catch (Exception ex) {
                    log("formula field " + item.getKey(), ex);
                }
            }
        }

        // Suppress.
        if (datafile.getSuppress() != null) {
            for (Map.Entry<String, Boolean> item : datafile.getSuppress().entrySet()) {
                applyObjectFormat(doc, item.getKey(), "suppress", item.getValue());
            }
        }

        // Can grow.
        if (datafile.getCanGrow() != null) {
            for (Map.Entry<String, Boolean> item : datafile.getCanGrow().entrySet()) {
                applyObjectFormat(doc, item.getKey(), "cangrow", item.getValue());
            }
        }

        // Resize (width).
        if (datafile.getResize() != null) {
            for (Map.Entry<String, Integer> item : datafile.getResize().entrySet()) {
                try {
                    IReportObject obj = doc.getReportDefController().findObjectByName(item.getKey());
                    if (obj != null) {
                        obj.setWidth(item.getValue());
                    }
                } catch (Exception ex) {
                    log("resize " + item.getKey(), ex);
                }
            }
        }

        // Object text. NOTE: Java RAS exposes ITextObject text via Paragraphs (no direct
        // setter equivalent to the .NET TextObject.Text). Replacement is applied best-effort.
        if (datafile.getObjectText() != null) {
            for (Map.Entry<String, String> item : datafile.getObjectText().entrySet()) {
                try {
                    ObjectTextSetter.setText(doc, item.getKey(), item.getValue());
                } catch (Exception ex) {
                    log("object text " + item.getKey(), ex);
                }
            }
        }

        // Sort (best-effort; see SortApplier).
        if (datafile.getSortByField() != null) {
            for (Map.Entry<String, String> item : datafile.getSortByField().entrySet()) {
                try {
                    SortApplier.apply(doc, item.getKey(), item.getValue());
                } catch (Exception ex) {
                    log("sort " + item.getKey(), ex);
                }
            }
        }

        // Move objects.
        if (datafile.getMoveObjectPosition() != null) {
            for (MoveObjects item : datafile.getMoveObjectPosition()) {
                try {
                    moveReportObject(doc, item);
                } catch (Exception ex) {
                    log("move object " + item.ObjectName, ex);
                }
            }
        }
    }

    private void applyObjectFormat(ReportClientDocument doc, String name, String which, boolean value) {
        try {
            IReportObject obj = doc.getReportDefController().findObjectByName(name);
            if (obj == null) {
                return;
            }
            IObjectFormat format = obj.getFormat();
            if ("suppress".equals(which)) {
                format.setEnableSuppress(value);
            } else if ("cangrow".equals(which)) {
                format.setEnableCanGrow(value);
            }
            obj.setFormat(format);
        } catch (Exception ex) {
            log(which + " " + name, ex);
        }
    }

    private void setFormulaText(ReportClientDocument doc, String name, String text) throws Exception {
        for (Object o : doc.getDataDefController().getDataDefinition().getFormulaFields()) {
            IFormulaField field = (IFormulaField) o;
            if (field.getName().equalsIgnoreCase(name)) {
                IFormulaField modified = (IFormulaField) field.clone(true);
                modified.setText(text);
                doc.getDataDefController().getFormulaFieldController().modify(field, modified);
                return;
            }
        }
    }

    private void setParameterValue(ReportClientDocument doc, String reportName, String name, Object val)
            throws Exception {
        ParameterFieldController controller = doc.getDataDefController().getParameterFieldController();
        for (Object o : doc.getDataDefController().getDataDefinition().getParameterFields()) {
            com.crystaldecisions.sdk.occa.report.data.IParameterField field =
                    (com.crystaldecisions.sdk.occa.report.data.IParameterField) o;
            if (field.getParameterType() != ParameterFieldType.reportParameter) {
                continue;
            }
            if (!field.getName().equalsIgnoreCase(name)) {
                continue;
            }
            String s = val == null ? null : val.toString();
            switch (field.getType().toVariantTypeString().toLowerCase()) {
                case "boolean":
                case "i1":
                    String b = isEmpty(s) ? "false" : s;
                    if ("0".equals(b)) {
                        b = "false";
                    } else if ("1".equals(b)) {
                        b = "true";
                    }
                    controller.setCurrentValue(reportName, name, Boolean.parseBoolean(b));
                    break;
                case "number":
                case "decimal":
                    String n = isEmpty(s) ? "0" : s;
                    try {
                        controller.setCurrentValue(reportName, name, Integer.parseInt(n));
                    } catch (NumberFormatException e) {
                        controller.setCurrentValue(reportName, name, Double.parseDouble(n));
                    }
                    break;
                case "date":
                case "datetime":
                    Date d = isEmpty(s) ? new Date() : parseIso8601DateTime(s);
                    controller.setCurrentValue(reportName, name, d);
                    break;
                default:
                    controller.setCurrentValue(reportName, name, isEmpty(s) ? "" : s);
                    break;
            }
            return;
        }
    }

    // The client libraries default to ISO 8601. Accept both offset/zoned forms (e.g.
    // .NET's round-trip "O" format, which includes an offset or trailing "Z") and the
    // bare local form this server has always accepted, treating an offset-less value
    // as UTC as before.
    private static Date parseIso8601DateTime(String s) {
        try {
            return Date.from(OffsetDateTime.parse(s).toInstant());
        } catch (DateTimeParseException ignored) {
            // not an offset/zoned ISO 8601 value; fall through to the local form
        }

        DateTimeFormatter f = new DateTimeFormatterBuilder()
                .append(DateTimeFormatter.ISO_LOCAL_DATE_TIME).toFormatter();
        return Date.from(LocalDateTime.parse(s, f).toInstant(ZoneOffset.ofHours(0)));
    }

    private void moveReportObject(ReportClientDocument doc, MoveObjects item) throws Exception {
        IReportObject control = doc.getReportDefController().findObjectByName(item.ObjectName);
        if (control == null) {
            return;
        }
        if (item.Type == MoveType.ABSOLUTE) {
            if (item.Pos == MovePosition.LEFT) {
                control.setLeft(item.Move);
            } else if (item.Pos == MovePosition.TOP) {
                control.setTop(item.Move);
            }
        } else {
            if (item.Pos == MovePosition.LEFT) {
                control.setLeft(control.getLeft() + item.Move);
            } else if (item.Pos == MovePosition.TOP) {
                control.setTop(control.getTop() + item.Move);
            }
        }
    }

    private void safeRemoveMainConnections(ReportClientDocument doc) {
        try {
            Connections conns = doc.getDatabase().getConnections();
            for (int i = 0; i < conns.size(); i++) {
                try {
                    doc.getDatabaseController().removeConnection(conns.getConnection(i));
                } catch (Exception ignored) {
                }
            }
        } catch (Exception ex) {
            log("remove main connections", ex);
        }
    }

    private void safeRemoveSubReportConnections(ReportClientDocument doc) {
        try {
            IStrings names = doc.getSubreportController().querySubreportNames();
            for (String name : names) {
                try {
                    ISubreportClientDocument sub = doc.getSubreportController().getSubreport(name);
                    Connections conns = sub.getDatabaseController().getDatabase().getConnections();
                    for (int i = 0; i < conns.size(); i++) {
                        sub.getDatabaseController().removeConnection(conns.getConnection(i));
                    }
                } catch (Exception ignored) {
                }
            }
        } catch (Exception ex) {
            log("remove subreport connections", ex);
        }
    }

    private static boolean isEmpty(String s) {
        return s == null || s.isEmpty();
    }

    private static boolean notBlank(String s) {
        return s != null && !s.trim().isEmpty();
    }

    private static void log(String what, Exception ex) {
        System.out.println("Exporter: failed to apply " + what + ": " + ex.getMessage());
    }
}
