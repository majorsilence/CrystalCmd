using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Majorsilence.CrystalCmd.Client
{
    public class Data
    {
        public Data()
        {
            Parameters = new Dictionary<string, object>();
            MoveObjectPosition = new List<MoveObjects>();
            DataTables = new Dictionary<string, string>();
            SubReportDataTables = new List<SubReports>();
        }

        // public byte[] ReportFile { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public IEnumerable<MoveObjects> MoveObjectPosition { get; set; }

        // DataTables converted to CSV, must be loaded into new DataTables

        public Dictionary<string, string> DataTables { get; set; }
        public List<SubReports> SubReportDataTables { get; set; }


        public void AddData(string name, DataTable dt)
        {
            string csv = DataTable2Csv(dt);
            DataTables.Add(name, csv);
        }

        public void AddData<T>(string name, IEnumerable<T> list)
        {
            string csv = GenericListToCsv(list);
            DataTables.Add(name, csv);
        }

        static string GenericListToCsv<T>(IEnumerable<T> list)
        {
            var sb = new StringBuilder();
            IEnumerable<string> columnNames = list.FirstOrDefault().GetType().GetProperties().
                                    Select(column => column.Name);
            sb.AppendLine(string.Join(",", columnNames));

            foreach (T tp in list)
            {
                IEnumerable<string> fields = typeof(T).GetProperties().Select(field =>
                 string.Concat("\"", field.GetValue(tp, null).ToString().Replace("\"", "\"\""), "\""));

                sb.AppendLine(string.Join(",", fields));
            }
            return sb.ToString();
        }


        static string DataTable2Csv(DataTable dt)
        {
            var sb = new StringBuilder();
            IEnumerable<string> columnNames = dt.Columns.Cast<DataColumn>().
                                              Select(column => column.ColumnName);
            sb.AppendLine(string.Join(",", columnNames));

            foreach (DataRow row in dt.Rows)
            {
                IEnumerable<string> fields = row.ItemArray.Select(field =>
                  string.Concat("\"", field.ToString().Replace("\"", "\"\""), "\""));

                sb.AppendLine(string.Join(",", fields));
            }

            return sb.ToString();
        }
    }
}
