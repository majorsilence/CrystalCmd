using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ChoETL;

namespace Majorsilence.CrystalCmd.RptEngine;

/// <summary>
/// Reads the client's CSV table format into a DataTable: a header row, a row of .NET type
/// names, then rows with every field quoted; byte[] columns as dash-separated hex or
/// base64. The same code as the Crystal worker's CsvReader (Server.Common), which is
/// .NET Framework only; kept identical so both backends read one request the same way.
/// </summary>
public static class CsvTableReader
{
    public static DataTable CreateTableEtl(string csv)
    {
        string[]? headers = null;
        string[]? columntypes = null;
        var dt = new DataTable();
        using (var reader = ChoCSVReader.LoadText(ConvertToWindowsEOL(csv), new ChoCSVRecordConfiguration()
        {
            // Bound the per-line buffer: 64 MB comfortably fits legitimate rows,
            // including base64/hex blob columns, while capping abuse.
            MaxLineSize = 64 * 1024 * 1024,
            // Tolerate case-insensitively duplicate header names rather than throwing;
            // in-process Crystal silently accepted such DataTables and bound to the first match.
            AutoIncrementDuplicateColumnNames = true,
        }).WithFirstLineHeader()
            .QuoteAllFields()
            .Configure(c => c.Encoding = Encoding.UTF8)
            .Configure(c => c.MayContainEOLInData = true))
        {
            int rowIdx = 0;
            ChoDynamicObject? e;

            while ((e = reader.Read()) != null)
            {
                if (rowIdx == 0)
                {
                    headers = e.Keys.ToArray();
                    columntypes = e.Values.Select(p => p?.ToString()?.Trim() ?? string.Empty).ToArray();
                    rowIdx++;

                    var altHeaders = e.AlternativeKeys?.ToArray();
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var currentAltHeader = headers[i];
                        if (altHeaders != null)
                        {
                            currentAltHeader = altHeaders[i].Value;
                        }
                        var headerToUse = currentAltHeader.Contains('.') ? currentAltHeader : headers[i];

                        var columnType = Type.GetType($"System.{columntypes[i]}", false, true);
                        if (columnType == null || columnType.Assembly != typeof(string).Assembly)
                        {
                            columnType = typeof(string);
                        }

                        dt.Columns.Add(headerToUse, columnType);
                    }
                    continue;
                }

                DataRow dr = dt.NewRow();
                var columns = e.Values.ToList();
                for (int i = 0; i < headers!.Length; i++)
                {
                    var cleaned = columns[i]?.ToString();
                    var columnType = dt.Columns[i].DataType;

                    if (columnType == typeof(string) && string.IsNullOrWhiteSpace(cleaned))
                    {
                        dr[i] = "";
                    }
                    else if (string.IsNullOrWhiteSpace(cleaned))
                    {
                        dr[i] = DBNull.Value;
                    }
                    else if (columnType == typeof(byte[]))
                    {
                        if (!TryParseByteArray(cleaned, out byte[]? bytesValue))
                        {
                            throw new FormatException($"Unable to parse '{cleaned}' as Byte[] for column '{headers[i]}' at row {rowIdx}.");
                        }
                        dr[i] = bytesValue!;
                    }
                    else if (columnType == typeof(DateTime))
                    {
                        if (!TryParseDateTime(cleaned, out DateTime dateValue))
                        {
                            throw new FormatException($"Unable to parse '{cleaned}' as DateTime for column '{headers[i]}' at row {rowIdx}.");
                        }
                        dr[i] = dateValue;
                    }
                    else
                    {
                        dr[i] = cleaned;
                    }
                }

                dt.Rows.Add(dr);
                rowIdx++;
            }
        }

        return dt;
    }

    private static bool TryParseDateTime(string value, out DateTime parsed)
    {
        // Trim whitespace and common surrounding wrapper characters that may appear
        // in exported CSV values (for example: <05/25/2026 9:35:38 AM> or "05/25/2026 ...").
        value = value?.Trim() ?? string.Empty;
        value = value.Trim('\\', '"', '\'', '<', '>', '[', ']', '(', ')');

        string[] formats =
        [
            "o",
            "s",
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy/MM/dd HH:mm:ss",
            "M/d/yyyy h:mm:ss tt",
            "MM/dd/yyyy h:mm:ss tt",
            "dd MMM yyyy",
            "dd MMM yyyy h:mm tt",
            "dddd, dd MMMM yyyy HH:mm:ss",
            "MM-dd-yyyy"
        ];

        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out parsed))
            return true;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out parsed))
            return true;
        return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out parsed);
    }

    private static bool TryParseByteArray(string value, out byte[]? parsed)
    {
        parsed = null;
        value = value?.Trim() ?? string.Empty;
        value = value.Trim('\\', '"', '\'', '<', '>', '[', ']', '(', ')');

        if (value.Length == 0)
        {
            parsed = [];
            return true;
        }

        // Dash-delimited hex, the format emitted by the official client via
        // BitConverter.ToString() (e.g. "4D-56-61"): unambiguous, so first.
        if (TryParseDashHex(value, out parsed))
            return true;

        // Base64, the format used by other producers that serialise byte[] with
        // Convert.ToBase64String(). Both must be tolerated rather than thrown on.
        try
        {
            parsed = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            parsed = null;
            return false;
        }
    }

    private static bool TryParseDashHex(string value, out byte[]? parsed)
    {
        parsed = null;
        string[] arr = value.Split('-');
        var array = new byte[arr.Length];
        for (int i = 0; i < arr.Length; i++)
        {
            var token = arr[i];
            if (token.Length == 0 || token.Length > 2)
                return false;
            foreach (char c in token)
            {
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }
            array[i] = Convert.ToByte(token, 16);
        }
        parsed = array;
        return true;
    }

    private static string ConvertToWindowsEOL(string readData)
    {
        // see https://stackoverflow.com/questions/31053/regex-c-replace-n-with-r-n for regex explanation
        return Regex.Replace(readData, "(?<!\r)\n", "\r\n");
    }
}
