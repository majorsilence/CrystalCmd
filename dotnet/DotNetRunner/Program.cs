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
                Parameters = new Dictionary<string, object>()
            };
            reportData.AddData("EMPLOYEE", dt);

            var list = GetList();
            var reportDataList = new Data()
            {
                DataTables = new Dictionary<string, string>(),
                MoveObjectPosition = new List<MoveObjects>(),
                Parameters = new Dictionary<string, object>()
            };
            reportDataList.AddData("Employee", list);

            await CreatePdfFromReport("the_dotnet_dataset_report.rpt", "the_dataset_report.pdf", reportData);

            await CreatePdfFromReport("the_dotnet_dataset_report.rpt", "the_dataset_report_ienumerable.pdf", reportDataList);

            await CreatePdfFromReport("thereport.rpt", "thereport.pdf", new Data());

            var parameterReportData = new Data();
            parameterReportData.Parameters.Add("MyParameter", "My First Parameter");
            parameterReportData.Parameters.Add("MyParameter2", "My Second Parameter");

            await CreatePdfFromReport("thereport_wth_parameters.rpt", "thereport_wth_parameters.pdf", parameterReportData);

            var subreportParameterData = new Data();
            subreportParameterData.SubReportParameters.Add(new SubReportParameters()
            {
                Parameters = parameterReportData.Parameters,
                ReportName = "thereport_wth_parameters.rpt"
            });
            await CreatePdfFromReport("thereport_with_subreport_with_parameters.rpt", "thereport_with_subreport_with_parameters.pdf", subreportParameterData);

            var subreportDatatableData = new Data();
            subreportDatatableData.AddData("the_dotnet_dataset_report.rpt", "Employee", dt);
            await CreatePdfFromReport("thereport_with_subreport_with_dotnet_dataset.rpt", "thereport_with_subreport_with_dotnet_dataset.pdf", subreportDatatableData);
        }

        private static async Task CreatePdfFromReport(string reportPath, string pdfOutputPath, Data reportData)
        {
            using (var fstream = new FileStream(reportPath, FileMode.Open))
            using (var fstreamOut = new FileStream(pdfOutputPath, FileMode.OpenOrCreate | FileMode.Append))
            {
                var rpt = new Report(exportUrl, username: "user", password: "password");
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
            table.Columns.Add("TestData", typeof(byte[]));

            // Here we add five DataRows.
            table.Rows.Add(25, "Indocin, Hi there", "David", DateTime.Now, System.Text.Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add(50, "Enebrel", "Sam", DateTime.Now, System.Text.Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add(10, "Hydralazine", "Christoff", DateTime.Now, System.Text.Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add(21, "Combivent", "Janet", DateTime.Now, System.Text.Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add(100, "Dilantin", "Melanie", DateTime.Now, System.Text.Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add(101, "Hello", "World", DateTime.Now, System.Text.Encoding.UTF8.GetBytes("Hello world"));
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
                LAST_NAME = "Indocin",
                TestData = System.Text.Encoding.UTF8.GetBytes("Hello world")
            }); 
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 50,
                FIRST_NAME = "Sam",
                LAST_NAME = "Enebrel",
                TestData = System.Text.Encoding.UTF8.GetBytes("Hello world")
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 10,
                FIRST_NAME = "Christoff",
                LAST_NAME = "Hydralazine",
                TestData = System.Text.Encoding.UTF8.GetBytes("Hello world")
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 21,
                FIRST_NAME = "Janet",
                LAST_NAME = "Combivent",
                TestData = System.Text.Encoding.UTF8.GetBytes("Hello world")
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 100,
                FIRST_NAME = "Melanie",
                LAST_NAME = "Dilantin",
                TestData = System.Text.Encoding.UTF8.GetBytes("Hello world")
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 101,
                FIRST_NAME = "Hello",
                LAST_NAME = "World",
                TestData = System.Text.Encoding.UTF8.GetBytes("Hello world")
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 102,
                FIRST_NAME = "IEnumerable",
                LAST_NAME = "List",
                TestData = System.Text.Encoding.UTF8.GetBytes("Hello world")
            });

            return list;
        }
    }
}
