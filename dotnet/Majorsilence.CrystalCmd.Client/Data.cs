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
        public IList<MoveObjects> MoveObjectPosition { get; set; }

        // DataTables converted to CSV, must be loaded into new DataTables

        public Dictionary<string, string> DataTables { get; set; }
        public IList<SubReports> SubReportDataTables { get; set; }


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

        public void AddData(string name, string rawCsv)
        {
            DataTables.Add(name, rawCsv);
        }


        public void AddData(string reportName, string subReportTableName, DataTable dt)
        {
            string csv = DataTable2Csv(dt);
            SubReportDataTables.Add(new SubReports()
            {
                DataTable = csv,
                ReportName = reportName,
                TableName = subReportTableName
            });
        }

        public void AddData<T>(string reportName, string subReportTableName, IEnumerable<T> list)
        {
            string csv = GenericListToCsv(list);
            SubReportDataTables.Add(new SubReports()
            {
                DataTable = csv,
                ReportName = reportName,
                TableName = subReportTableName
            });
        }

        static string GenericListToCsv<T>(IEnumerable<T> list)
        {
            var sb = new StringBuilder();
            IEnumerable<string> columnNames = list.FirstOrDefault().GetType().GetProperties().
                                    Select(column => column.Name);
            sb.AppendLine(string.Join(",", columnNames));

            IEnumerable<string> columnTypes = list.FirstOrDefault().GetType().GetProperties().
                                        Select(column => column.PropertyType.Name.ToString());
            sb.AppendLine(string.Join(",", columnTypes));

            foreach (T tp in list)
            {
                IList<string> fields = new List<string>();
                foreach (var field in typeof(T).GetProperties()) {

                    var value = field.GetValue(tp, null);

                    if (value?.GetType() == typeof(byte[]))
                    {
                        fields.Add(string.Concat("\"", BitConverter.ToString((byte[])value), "\""));
                    }
                    else
                    {
                        fields.Add(string.Concat("\"", value?.ToString()?.Replace("\"", "\"\""), "\""));
                    }
                      
                 
                }

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

            IEnumerable<string> columnTypes = dt.Columns.Cast<DataColumn>().
                                             Select(column => column.DataType.Name);
            sb.AppendLine(string.Join(",", columnTypes));

            foreach (DataRow row in dt.Rows)
            {
                //BitConverter.ToString("heloo");

                IList<string> fields = new List<string>();

                foreach (var field in row.ItemArray)
                {
                    if (field.GetType() == typeof(byte[]))
                    {
                        fields.Add(string.Concat("\"", BitConverter.ToString((byte[])field), "\""));
                    }
                    else
                    {
                        fields.Add(string.Concat("\"", field.ToString().Replace("\"", "\"\""), "\""));
                    }
                }
                sb.AppendLine(string.Join(",", fields));
            }

            return sb.ToString();
        }
    }
}
