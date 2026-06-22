using ChoETL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server.Common
{
    public static class CsvReader
    {
        public static DataTable CreateTableEtl(string csv)
        {
            string[] headers = null;
            string[] columntypes = null;
            DataTable dt = new DataTable();
            using (var reader = ChoCSVReader.LoadText(ConvertToWindowsEOL(csv), new ChoCSVRecordConfiguration()
            {
                MaxLineSize = int.MaxValue / 5,

                // Tolerate case-insensitively duplicate header names rather
                // than throwing ChoRecordConfigurationException. In-process Crystal
                // silently accepted such DataTables and bound to the first match; 
                AutoIncrementDuplicateColumnNames = true,
            }).WithFirstLineHeader()
                .QuoteAllFields()
                .Configure(c => c.Encoding = Encoding.UTF8)
                .Configure(c => c.MayContainEOLInData = true)
                )
            {
                int rowIdx = 0;
                ChoDynamicObject e;

                while ((e = reader.Read()) != null)
                {
                    if (rowIdx == 0)
                    {
                        headers = e.Keys.ToArray();
                        columntypes = e.Values.Select(p => p?.ToString()?.Trim() ?? string.Empty).ToArray();
                        rowIdx = rowIdx + 1;

                        var altHeaders = e.AlternativeKeys?.ToArray();
                        for (int i = 0; i < headers.Length; i++)
                        {
                            var currentAltHeader = headers[i];
                            if (altHeaders != null)
                            {
                                currentAltHeader = altHeaders[i].Value;
                            }
                            var headerToUse = currentAltHeader.Contains(".") ? currentAltHeader : headers[i];

                            var columnType = Type.GetType($"System.{columntypes[i]}", false, true);
                            if (columnType == null || columnType.Assembly != typeof(string).Assembly)
                            {
                                columnType = typeof(System.String);
                            }

                            dt.Columns.Add(headerToUse, columnType);
                        }
                        continue;
                    }

                    DataRow dr = dt.NewRow();

                        var columns = e.Values.ToList();
                    for (int i = 0; i < headers.Length; i++)
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
                            if (!TryParseByteArray(cleaned, out byte[] bytesValue))
                            {
                                throw new FormatException($"Unable to parse '{cleaned}' as Byte[] for column '{headers[i]}' at row {rowIdx}.");
                            }

                            dr[i] = bytesValue;
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
                    rowIdx = rowIdx + 1;
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

            string[] formats = new[]
            {
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
            };

            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out parsed))
            {
                return true;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out parsed))
            {
                return true;
            }

            return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out parsed);
        }

        private static bool TryParseByteArray(string value, out byte[] parsed)
        {
            parsed = null;

            value = value?.Trim() ?? string.Empty;
            value = value.Trim('\\', '"', '\'', '<', '>', '[', ']', '(', ')');

            if (value.Length == 0)
            {
                parsed = new byte[0];
                return true;
            }

            // Dash-delimited hex, the format emitted by the official client via
            // BitConverter.ToString() (e.g. "4D-56-61"). Try this first because it
            // is unambiguous and what Data.DataTable2Csv produces.
            if (TryParseDashHex(value, out parsed))
            {
                return true;
            }

            // Base64, the format used by other producers that serialise byte[]
            // with Convert.ToBase64String() (e.g. "MVaoMDzqATjm/UaXioVCqg==").
            // In-process Crystal accepted whatever byte[] it was handed, so the
            // server must tolerate both encodings rather than throwing.
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

        private static bool TryParseDashHex(string value, out byte[] parsed)
        {
            parsed = null;

            String[] arr = value.Split('-');
            var array = new byte[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                var token = arr[i];
                if (token.Length == 0 || token.Length > 2)
                {
                    return false;
                }

                foreach (char c in token)
                {
                    bool isHex = (c >= '0' && c <= '9')
                        || (c >= 'a' && c <= 'f')
                        || (c >= 'A' && c <= 'F');
                    if (!isHex)
                    {
                        return false;
                    }
                }

                array[i] = Convert.ToByte(token, 16);
            }

            parsed = array;
            return true;
        }

        private static string ConvertToWindowsEOL(string readData)
        {
            // see https://stackoverflow.com/questions/31053/regex-c-replace-n-with-r-n for regex explanation
            readData = Regex.Replace(readData, "(?<!\r)\n", "\r\n");
            return readData;
        }
    }
}