using ChoETL;
using System;
using System.Collections.Generic;
using System.Data;
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
                        columntypes = e.Values.Select(p => p.ToString()).ToArray();
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
                        if (string.Equals(columntypes[i], "string", StringComparison.InvariantCultureIgnoreCase) && string.IsNullOrWhiteSpace(cleaned))
                        {
                            dr[i] = "";
                        }
                        else if (string.IsNullOrWhiteSpace(cleaned))
                        {
                            dr[i] = DBNull.Value;
                        }
                        else if (string.Equals(columntypes[i], "byte[]", StringComparison.InvariantCultureIgnoreCase))
                        {
                            dr[i] = HexStringToByteArray(cleaned);
                        }
                        else
                        {

                            dr[i] = cleaned;
                        }
                    }

                    dt.Rows.Add(dr);
                }
            }

            return dt;
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
