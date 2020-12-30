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
        static string exportUrl = "https://localhost:44355/export";
        public static async Task Main(string[] args)
        {
            CreateJsonForConsoleRunner();
            await ConnectToServerWritePdfAsync();
        }

        static void CreateJsonForConsoleRunner()
        {
            DataTable dt = GetTable();

            var reportData = new Data()
            {
                DataTables = new Dictionary<string, string>(),
                MoveObjectPosition = new List<MoveObjects>(),
                Parameters = new Dictionary<string, object>(),
                //ReportFile = File.ReadAllBytes("thereport.rpt")
            };
            reportData.AddData("Employee", dt);

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(reportData);
            File.WriteAllText("test.json", json);
        }


        static async Task ConnectToServerWritePdfAsync()
        {
            DataTable dt = GetTable();
            var reportData = new Data()
            {
                DataTables = new Dictionary<string, string>(),
                MoveObjectPosition = new List<MoveObjects>(),
                Parameters = new Dictionary<string, object>(),
                //ReportFile = File.ReadAllBytes("the_dataset_report.rpt")
            };
            reportData.AddData("Employee", dt);

            var list = GetList();
            var reportDataList = new Data()
            {
                DataTables = new Dictionary<string, string>(),
                MoveObjectPosition = new List<MoveObjects>(),
                Parameters = new Dictionary<string, object>(),
                //ReportFile = File.ReadAllBytes("thereport.rpt")
            };
            reportDataList.AddData("Employee", list);





            //using (var fstreamOut = new FileStream("the_dataset_report.pdf", FileMode.OpenOrCreate | FileMode.Append))
            //{
            //    var rpt = new Majorsilence.CrystalCmd.Client.Report(exportUrl);
            //    using (var stream = await rpt.GenerateAsync(reportData))
            //    {
            //        stream.CopyTo(fstreamOut);
            //    }
            //}



            // report data



            using (var fstream = new FileStream("the_dataset_report.rpt", FileMode.Open))
            using (var fstreamOut = new FileStream("the_dataset_report.pdf", FileMode.OpenOrCreate | FileMode.Append))
            {
                var rpt = new Majorsilence.CrystalCmd.Client.Report(exportUrl, username:"user", password: "password");
                using (var stream = await rpt.GenerateAsync(reportData, fstream))
                {
                    stream.CopyTo(fstreamOut);
                }
            }


            using (var fstream = new FileStream("the_dataset_report.rpt", FileMode.Open))
            using (var fstreamOut = new FileStream("the_dataset_report_ienumerable.pdf", FileMode.OpenOrCreate | FileMode.Append))
            {
                var rpt = new Majorsilence.CrystalCmd.Client.Report(exportUrl);
                using (var stream = await rpt.GenerateAsync(reportDataList, fstream))
                {
                    stream.CopyTo(fstreamOut);
                }
            }

            using (var fstream = new FileStream("thereport.rpt", FileMode.Open))
            using (var fstreamOut = new FileStream("thereport.pdf", FileMode.OpenOrCreate | FileMode.Append))
            {
                var rpt = new Majorsilence.CrystalCmd.Client.Report(exportUrl);
                using (var stream = await rpt.GenerateAsync(new Data(), fstream))
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
            table.Rows.Add(25, "Indocin, Hi there", "David", DateTime.Now);
            table.Rows.Add(50, "Enebrel", "Sam", DateTime.Now);
            table.Rows.Add(10, "Hydralazine", "Christoff", DateTime.Now);
            table.Rows.Add(21, "Combivent", "Janet", DateTime.Now);
            table.Rows.Add(100, "Dilantin", "Melanie", DateTime.Now);
            table.Rows.Add(101, "Hello", "World", DateTime.Now);
            return table;
        }

        static IEnumerable<Employee> GetList()
        {
            // Here we create a DataTable with four columns.
            var list = new List<Employee>();

            // Here we add five DataRows.
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 25,
                FIRST_NAME = "David",
                LAST_NAME = "Indocin"
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 50,
                FIRST_NAME = "Sam",
                LAST_NAME = "Enebrel"
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 10,
                FIRST_NAME = "Christoff",
                LAST_NAME = "Hydralazine"
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 21,
                FIRST_NAME = "Janet",
                LAST_NAME = "Combivent"
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 100,
                FIRST_NAME = "Melanie",
                LAST_NAME = "Dilantin"
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 101,
                FIRST_NAME = "Hello",
                LAST_NAME = "World"
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 102,
                FIRST_NAME = "IEnumerable",
                LAST_NAME = "List"
            });

            return list;
        }
    }
}
