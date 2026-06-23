import com.crystaldecisions.sdk.occa.report.application.ReportClientDocument;

/**
 * Data.ObjectText support.
 *
 * The .NET engine exposes a writable TextObject.Text; the Java RAS SDK in lib/ exposes
 * ITextObject with only getText() (read-only) — replacing a text object's content requires
 * rebuilding its Paragraphs/ParagraphElements, which this SDK build does not reliably
 * support. Rather than ship a guess that silently does nothing, this logs a clear message.
 * Use a formula field (Data.FormulaFieldText) for dynamic text instead.
 */
public final class ObjectTextSetter {

    private ObjectTextSetter() {
    }

    static void setText(ReportClientDocument doc, String objectName, String text) {
        System.out.println("Exporter: ObjectText is not supported by the Java RAS SDK "
                + "(ITextObject is read-only); skipped object '" + objectName
                + "'. Use a formula field (FormulaFieldText) for dynamic text.");
    }
}
