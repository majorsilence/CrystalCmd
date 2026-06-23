import com.crystaldecisions.sdk.occa.report.application.ReportClientDocument;
import com.crystaldecisions.sdk.occa.report.data.IField;
import com.crystaldecisions.sdk.occa.report.data.ISort;
import com.crystaldecisions.sdk.occa.report.data.ITable;
import com.crystaldecisions.sdk.occa.report.data.Sort;
import com.crystaldecisions.sdk.occa.report.data.SortDirection;

/**
 * Applies a record sort (Data.SortByField: tableName -> fieldName), the Java counterpart of
 * the C# SetSortOrder. Best-effort: replaces the first record sort if one exists, otherwise
 * adds it.
 */
public final class SortApplier {

    private SortApplier() {
    }

    static void apply(ReportClientDocument doc, String tableName, String fieldName) throws Exception {
        IField field = findField(doc, tableName, fieldName);
        if (field == null) {
            System.out.println("Exporter: sort field not found " + tableName + "." + fieldName);
            return;
        }
        Sort sort = new Sort();
        sort.setSortField(field);
        sort.setDirection(SortDirection.ascendingOrder);

        ISort newSort = sort;
        try {
            doc.getDataDefController().getRecordSortController().modify(0, newSort);
        } catch (Exception replaceFailed) {
            doc.getDataDefController().getRecordSortController().add(0, newSort);
        }
    }

    private static IField findField(ReportClientDocument doc, String tableName, String fieldName) throws Exception {
        for (ITable table : doc.getDatabase().getTables()) {
            if (!table.getName().equalsIgnoreCase(tableName)) {
                continue;
            }
            for (IField f : table.getDataFields()) {
                if (f.getName().equalsIgnoreCase(fieldName)) {
                    return f;
                }
            }
        }
        return null;
    }
}
