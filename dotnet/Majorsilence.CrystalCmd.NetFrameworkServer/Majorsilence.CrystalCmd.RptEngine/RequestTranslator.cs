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

        int tableCount = data.DataTables.Count + data.EmptyDataTables.Count;
        if (tableCount > 1)
            throw new NotServiceableException(
                $"The request carries {tableCount} tables; this backend renders a report from one flattened table (roadmap stage 3 adds table links).");

        var overrides = new RuntimeOverrides { RecordSelectionFormula = data.RecordSelectionFormula };

        if (data.DataTables.Count == 1)
        {
            var (name, csv) = (data.DataTables.Keys.First(), data.DataTables.Values.First());
            CheckTableName(name, analysis.DataTables, warnings, "DataTables");
            overrides.Data = CsvTableReader.CreateTableEtl(csv);
        }
        else if (data.EmptyDataTables.Count == 1)
        {
            string name = data.EmptyDataTables[0];
            CheckTableName(name, analysis.DataTables, warnings, "EmptyDataTables");
            overrides.Data = EmptyTable(name, analysis.DataTables, warnings, "EmptyDataTables");
        }

        SubreportTables(data, analysis, overrides, warnings);

        foreach (var kv in data.Parameters)
            overrides.Parameters[kv.Key] = kv.Value;

        foreach (var sp in data.SubReportParameters)
        {
            if (sp.Parameters is null || sp.Parameters.Count == 0) continue;
            string? target = ResolveSubreportName(sp.ReportName, analysis, warnings, "SubReportParameters");
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

    // Tables pushed to subreports, each resolved to the subreport the template names the way
    // SubReportParameters are. A subreport renders from one flattened table, as the main
    // report does, so a second table for the same subreport is refused. An empty table gets
    // the subreport's own columns, as the main report's does.
    private static void SubreportTables(Data data, ReportAnalysis analysis, RuntimeOverrides overrides, List<string> warnings)
    {
        var pushed = data.SubReportDataTables.Select(s => (Sub: s, Empty: false))
            .Concat(data.EmptySubReportDataTables.Select(s => (Sub: s, Empty: true)));
        foreach (var (sub, empty) in pushed)
        {
            string? target = ResolveSubreportName(sub.ReportName, analysis, warnings, "SubReportDataTables");
            if (target is null) continue;
            if (overrides.SubreportData.ContainsKey(target))
                throw new NotServiceableException(
                    $"The request pushes more than one table to subreport '{target}'; this backend renders a subreport from one flattened table.");

            var tables = analysis.Subreports.First(s => s.SubreportName == target).DataTables;
            if (!string.IsNullOrEmpty(sub.TableName))
                CheckTableName(sub.TableName, tables, warnings, $"SubReportDataTables ({target})");
            overrides.SubreportData[target] = empty || sub.DataTable is null
                ? EmptyTable(sub.TableName ?? string.Empty, tables, warnings, $"EmptySubReportDataTables ({target})")
                : CsvTableReader.CreateTableEtl(sub.DataTable);
        }
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
    private static void CheckTableName(string name, IReadOnlyList<DataTableAnalysis> tables, List<string> warnings, string label)
    {
        if (tables.Count == 0) return;
        if (FindTable(name, tables) is null)
            warnings.Add($"{label}: the report has no table named '{name}' (it has {string.Join(", ", tables.Select(t => t.TableName))}); the data is pushed to the report's single table anyway");
    }

    private static DataTableAnalysis? FindTable(string name, IReadOnlyList<DataTableAnalysis> tables)
    {
        if (int.TryParse(name, out int index))
            return index >= 1 && index <= tables.Count ? tables[index - 1] : null;
        return tables.FirstOrDefault(t => string.Equals(t.TableName, name, StringComparison.OrdinalIgnoreCase));
    }

    // An empty table with the template's own columns, so every field resolves and prints
    // nothing, which is what the Crystal runtime's callers get from an empty DataTable
    // built from the report's schema. Column types are not in the analysis; strings do.
    private static DataTable EmptyTable(string name, IReadOnlyList<DataTableAnalysis> tables, List<string> warnings, string label)
    {
        var table = FindTable(name, tables) ?? (tables.Count == 1 ? tables[0] : null);
        var dt = new DataTable();
        if (table is null)
        {
            warnings.Add($"{label}: no columns known for '{name}'; the report renders with no data");
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
    private static string? ResolveSubreportName(string requested, ReportAnalysis analysis, List<string> warnings, string label)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            warnings.Add($"{label}: an entry has no ReportName and is skipped");
            return null;
        }
        string bare = requested.EndsWith(".rpt", StringComparison.OrdinalIgnoreCase) ? requested[..^4] : requested;
        var hit = analysis.Subreports.FirstOrDefault(s => string.Equals(s.SubreportName, requested, StringComparison.OrdinalIgnoreCase))
               ?? analysis.Subreports.FirstOrDefault(s => string.Equals(s.SubreportName, bare, StringComparison.OrdinalIgnoreCase));
        if (hit is not null) return hit.SubreportName;
        if (analysis.Subreports.Count == 1)
        {
            warnings.Add($"{label}: no subreport named '{requested}'; applied to the report's only subreport, '{analysis.Subreports[0].SubreportName}'");
            return analysis.Subreports[0].SubreportName;
        }
        warnings.Add($"{label}: no subreport named '{requested}' (the report has {(analysis.Subreports.Count == 0 ? "none" : string.Join(", ", analysis.Subreports.Select(s => s.SubreportName)))}); skipped");
        return null;
    }
}
