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
                            dr[i] = HexStringToByteArray(cleaned);
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
            value = value?.Trim().Trim('\\', '"') ?? string.Empty;

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

        private static byte[] HexStringToByteArray(string cleaned)
        {
            String[] arr = cleaned.Split('-');
            byte[] array = new byte[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                array[i] = Convert.ToByte(arr[i], 16);
            }
            return array;
        }

        private static string ConvertToWindowsEOL(string readData)
        {
            // see https://stackoverflow.com/questions/31053/regex-c-replace-n-with-r-n for regex explanation
            readData = Regex.Replace(readData, "(?<!\r)\n", "\r\n");
            return readData;
        }
    }
}