using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace DotNetRunner
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            CreateJsonForConsoleRunner ();
            ConnectToServerWritePdf ();
        }

        static void CreateJsonForConsoleRunner(){
            DataTable dt = GetTable ();
            string csv = DataTable2Csv (dt);

            var reportData = new Data () {
                DataTables = new Dictionary<string, string> (),
                MoveObjectPosition = new List<MoveObjects> (),
                Parameters = new Dictionary<string, object> (),
                //ReportFile = File.ReadAllBytes("thereport.rpt")
            };
            reportData.DataTables.Add ("Employee", csv);

            string json = Newtonsoft.Json.JsonConvert.SerializeObject (reportData);
            File.WriteAllText ("test.json", json);
        }


        static void ConnectToServerWritePdf(){
            DataTable dt = GetTable ();
            string csv = DataTable2Csv (dt);

            var reportData = new Data () {
                DataTables = new Dictionary<string, string> (),
                MoveObjectPosition = new List<MoveObjects> (),
                Parameters = new Dictionary<string, object> (),
                //ReportFile = File.ReadAllBytes("thereport.rpt")
            };
            reportData.DataTables.Add ("Employee", csv);


            // report data
            string json = Newtonsoft.Json.JsonConvert.SerializeObject (reportData);

            // crystal report                   
            var crystalReport = System.IO.File.ReadAllBytes ("the_dataset_report.rpt");

            /*
             string crystalReport = Convert.ToBase64String (System.IO.File.ReadAllBytes ("the_dataset_report.rpt"));

            using (var client = new WebClient ()) {
                var data = new NameValueCollection ();
                data.Add ("reportdata", json);
                data.Add ("reporttemplate", crystalReport);
                var result = client.UploadValues ("https://c.majorsilence.com/export", data);

                System.IO.File.WriteAllBytes ("test_report_from_server.pdf", result);
            }  
            */

            HttpClient httpClient = new HttpClient ();
            MultipartFormDataContent form = new MultipartFormDataContent ();

            form.Add (new StringContent (json), "reportdata");
            form.Add (new ByteArrayContent (crystalReport), "reporttemplate", "the_dataset_report.rpt");
            HttpResponseMessage response = httpClient.PostAsync ("https://c.majorsilence.com/export", form).Result;

            response.EnsureSuccessStatusCode ();
            httpClient.Dispose ();
            var result = response.Content.ReadAsByteArrayAsync ().Result;
            //string sd = response.Content.ReadAsStringAsync ().Result;
            System.IO.File.WriteAllBytes ("test_report_from_server.pdf", result);
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
