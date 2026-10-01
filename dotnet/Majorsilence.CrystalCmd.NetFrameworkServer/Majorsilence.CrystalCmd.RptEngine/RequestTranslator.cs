using System.Data;
using Majorsilence.Crystal.RptEngine;
using Majorsilence.Crystal.Runtime;
using Majorsilence.CrystalCmd.Common;

namespace Majorsilence.CrystalCmd.RptEngine;

/// <summary>A request translated into what the engine takes, plus what was skipped on the way.</summary>
public sealed class TranslatedRequest
{
    public required RuntimeOverrides Overrides { get; init; }
    public required ExportFormat Format { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>
/// Turns a CrystalCmd request (<see cref="Data"/>) into the engine's
/// <see cref="RuntimeOverrides"/> and <see cref="ExportFormat"/>, with the conventions the
/// Crystal Reports worker's callers rely on: tables by name or 1-based index, matched
/// case-insensitively; CSV tables in the client's header/type/rows format; an empty table
/// built from the template's own columns; subreports named as the client names them. What
/// the engine cannot take is refused up front with the rule it breaks
/// (<see cref="NotServiceableException"/>); what it can take but will not use is a warning.
/// </summary>
public static class RequestTranslator
{
    public static TranslatedRequest Translate(Data data, ReportAnalysis analysis)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        if (analysis is null) throw new ArgumentNullException(nameof(analysis));
        var warnings = new List<string>();

        var format = MapFormat(data.ExportAs);

        if (data.SubReportDataTables.Count > 0 || data.EmptySubReportDataTables.Count > 0)
            throw new NotServiceableException(
                "The request pushes data to a subreport; this backend cannot hand a subreport its own data yet (roadmap stage 3).");

        int tableCount = data.DataTables.Count + data.EmptyDataTables.Count;
        if (tableCount > 1)
            throw new NotServiceableException(
                $"The request carries {tableCount} tables; this backend renders a report from one flattened table (roadmap stage 3 adds table links).");

        var overrides = new RuntimeOverrides { RecordSelectionFormula = data.RecordSelectionFormula };

        if (data.DataTables.Count == 1)
        {
            var (name, csv) = (data.DataTables.Keys.First(), data.DataTables.Values.First());
            CheckTableName(name, analysis, warnings);
            overrides.Data = CsvTableReader.CreateTableEtl(csv);
        }
        else if (data.EmptyDataTables.Count == 1)
        {
            string name = data.EmptyDataTables[0];
            CheckTableName(name, analysis, warnings);
            overrides.Data = EmptyTable(name, analysis, warnings);
        }

        foreach (var kv in data.Parameters)
            overrides.Parameters[kv.Key] = kv.Value;

        foreach (var sp in data.SubReportParameters)
        {
            if (sp.Parameters is null || sp.Parameters.Count == 0) continue;
            string? target = ResolveSubreportName(sp.ReportName, analysis, warnings);
            if (target is null) continue;
            if (!overrides.SubreportParameters.TryGetValue(target, out var bag))
                overrides.SubreportParameters[target] = bag = new Dictionary<string, object?>();
            foreach (var kv in sp.Parameters)
                bag[kv.Key] = kv.Value;
        }

        foreach (var kv in data.Suppress) overrides.Suppress[kv.Key] = kv.Value;
        foreach (var kv in data.CanGrow) overrides.CanGrow[kv.Key] = kv.Value;
        foreach (var kv in data.Resize) overrides.Resize[kv.Key] = kv.Value;
        foreach (var kv in data.ObjectText) overrides.ObjectText[kv.Key] = kv.Value;
        foreach (var kv in data.FormulaFieldText) overrides.FormulaFieldText[kv.Key] = kv.Value;

        if (data.SortByField.Count > 0)
        {
            var first = data.SortByField.First();
            overrides.SortByFieldName = $"{first.Key}.{first.Value}";
            if (data.SortByField.Count > 1)
                warnings.Add($"SortByField: {data.SortByField.Count} entries given; only the first ({first.Key}.{first.Value}) applies, as the report has one primary sort");
        }

        foreach (var move in data.MoveObjectPosition)
        {
            overrides.MoveObjectPosition.Add(new MoveObjectOverride
            {
                ObjectName = move.ObjectName,
                Axis = move.Pos == MovePosition.TOP ? MoveAxis.Top : MoveAxis.Left,
                Amount = move.Move,
                Relative = move.Type == MoveType.RELATIVE
            });
        }

        return new TranslatedRequest { Overrides = overrides, Format = format, Warnings = warnings };
    }

    /// <summary>The engine's format for a request's export type, or a refusal for the three with no equivalent.</summary>
    public static ExportFormat MapFormat(ExportTypes exportAs) => exportAs switch
    {
        ExportTypes.PDF => ExportFormat.Pdf,
        ExportTypes.CSV => ExportFormat.Csv,
        ExportTypes.Excel => ExportFormat.Excel,
        ExportTypes.ExcelDataOnly => ExportFormat.ExcelDataOnly,
        ExportTypes.RichText => ExportFormat.Rtf,
        ExportTypes.CrystalReport => throw new NotServiceableException("Export type CrystalReport returns the template itself; this backend has no equivalent."),
        ExportTypes.TEXT => throw new NotServiceableException("Export type TEXT has no equivalent on this backend."),
        ExportTypes.WordDoc => throw new NotServiceableException("Export type WordDoc has no equivalent on this backend; RichText is the nearest, and Word opens it."),
        _ => throw new NotServiceableException($"Export type {exportAs} is not known to this backend.")
    };

    // The engine has one flattened table, so whatever table the caller named receives the
    // data. The name is still checked against the template, as the Crystal runtime's
    // callers are told when a table is not found.
    private static void CheckTableName(string name, ReportAnalysis analysis, List<string> warnings)
    {
        if (analysis.DataTables.Count == 0) return;
        if (FindTable(name, analysis) is null)
            warnings.Add($"DataTables: the report has no table named '{name}' (it has {string.Join(", ", analysis.DataTables.Select(t => t.TableName))}); the data is pushed to the report's single table anyway");
    }

    private static DataTableAnalysis? FindTable(string name, ReportAnalysis analysis)
    {
        if (int.TryParse(name, out int index))
            return index >= 1 && index <= analysis.DataTables.Count ? analysis.DataTables[index - 1] : null;
        return analysis.DataTables.FirstOrDefault(t => string.Equals(t.TableName, name, StringComparison.OrdinalIgnoreCase));
    }

    // An empty table with the template's own columns, so every field resolves and prints
    // nothing, which is what the Crystal runtime's callers get from an empty DataTable
    // built from the report's schema. Column types are not in the analysis; strings do.
    private static DataTable EmptyTable(string name, ReportAnalysis analysis, List<string> warnings)
    {
        var table = FindTable(name, analysis) ?? (analysis.DataTables.Count == 1 ? analysis.DataTables[0] : null);
        var dt = new DataTable();
        if (table is null)
        {
            warnings.Add($"EmptyDataTables: no columns known for '{name}'; the report renders with no data");
            return dt;
        }
        foreach (var column in table.ColumnNames)
            dt.Columns.Add(column, typeof(string));
        return dt;
    }

    // Callers name a subreport the way the Crystal runtime does, usually the imported
    // file's name with or without ".rpt"; the template records the placed object's name
    // ("Subreport1"). Exact first, then without the extension, then the only subreport
    // there is; otherwise a warning and the values are skipped.
    private static string? ResolveSubreportName(string requested, ReportAnalysis analysis, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            warnings.Add("SubReportParameters: an entry has no ReportName and is skipped");
            return null;
        }
        string bare = requested.EndsWith(".rpt", StringComparison.OrdinalIgnoreCase) ? requested[..^4] : requested;
        var hit = analysis.Subreports.FirstOrDefault(s => string.Equals(s.SubreportName, requested, StringComparison.OrdinalIgnoreCase))
               ?? analysis.Subreports.FirstOrDefault(s => string.Equals(s.SubreportName, bare, StringComparison.OrdinalIgnoreCase));
        if (hit is not null) return hit.SubreportName;
        if (analysis.Subreports.Count == 1)
        {
            warnings.Add($"SubReportParameters: no subreport named '{requested}'; applied to the report's only subreport, '{analysis.Subreports[0].SubreportName}'");
            return analysis.Subreports[0].SubreportName;
        }
        warnings.Add($"SubReportParameters: no subreport named '{requested}' (the report has {(analysis.Subreports.Count == 0 ? "none" : string.Join(", ", analysis.Subreports.Select(s => s.SubreportName)))}); skipped");
        return null;
    }
}
