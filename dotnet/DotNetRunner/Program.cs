using Majorsilence.CrystalCmd.Client;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DotNetRunner
{
    class MainClass
    {
        public static async Task Main(string[] args)
        {
            CreateJsonForConsoleRunner();
            await ConnectToServerWritePdfAsync();
        }

        static void CreateJsonForConsoleRunner()
        {
            DataTable dt = GetTable();
            string csv = DataTable2Csv(dt);

            var reportData = new Data()
            {
                DataTables = new Dictionary<string, string>(),
                MoveObjectPosition = new List<MoveObjects>(),
                Parameters = new Dictionary<string, object>(),
                //ReportFile = File.ReadAllBytes("thereport.rpt")
            };
            reportData.DataTables.Add("Employee", csv);

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(reportData);
            File.WriteAllText("test.json", json);
        }


        static async Task ConnectToServerWritePdfAsync()
        {
            DataTable dt = GetTable();
            string csv = DataTable2Csv(dt);

            var reportData = new Data()
            {
                DataTables = new Dictionary<string, string>(),
                MoveObjectPosition = new List<MoveObjects>(),
                Parameters = new Dictionary<string, object>(),
                //ReportFile = File.ReadAllBytes("thereport.rpt")
            };
            reportData.DataTables.Add("Employee", csv);


            // report data


            // crystal report                   
            var crystalReport = System.IO.File.ReadAllBytes("the_dataset_report.rpt");


            using (var fstream = new FileStream("the_dataset_report.rpt", FileMode.Open))
            using (var fstreamOut = new FileStream("test_report_from_server.pdf", FileMode.OpenOrCreate | FileMode.Append))
            {
                var rpt = new Majorsilence.CrystalCmd.Client.Report();
                using (var stream = await rpt.GenerateAsync(reportData, fstream))
                {
                    stream.CopyTo(fstreamOut);
                }
            }


        }

        static DataTable GetTable()
        {
            // Here we create a DataTable with four columns.
            DataTable table = new DataTable();
            table.Columns.Add("EMPLOYEE_ID", typeof(int));
            table.Columns.Add("LAST_NAME", typeof(string));
            table.Columns.Add("FIRST_NAME", typeof(string));
            table.Columns.Add("BIRTH_DATE", typeof(DateTime));

            // Here we add five DataRows.
            table.Rows.Add(25, "Indocin", "David", DateTime.Now);
            table.Rows.Add(50, "Enebrel", "Sam", DateTime.Now);
            table.Rows.Add(10, "Hydralazine", "Christoff", DateTime.Now);
            table.Rows.Add(21, "Combivent", "Janet", DateTime.Now);
            table.Rows.Add(100, "Dilantin", "Melanie", DateTime.Now);
            table.Rows.Add(101, "Hello", "World", DateTime.Now);
            return table;
        }

        static string DataTable2Csv(DataTable dt)
        {
            var sb = new StringBuilder();

            // column names
            IEnumerable<string> columnNames = dt.Columns.Cast<DataColumn>().
                                              Select(column => column.ColumnName);
            sb.AppendLine(string.Join(",", columnNames));

            /*
            foreach (DataRow row in dt.Rows)
            {
                IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString());
                sb.AppendLine(string.Join(",", fields));
            }
            */


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
