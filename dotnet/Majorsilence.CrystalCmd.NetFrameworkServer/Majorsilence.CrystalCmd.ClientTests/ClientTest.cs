using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using Majorsilence.CrystalCmd.Common;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Linq;

namespace Majorsilence.CrystalCmd.ClientTests
{
    [TestFixture]
    public class ClientTest
    {
        private const string baseUrl = "http://localhost:44355/";
        private const string username = "user";
        private const string password = "password";
        private const string bearerTokenKey = "PLACEHOLDER_PLACEHOLDER_PLACEHOLDER_PLACEHOLDER"; // match appsettings.json

        [SetUp]
        public void Setup()
        {
            var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.pdf");
            foreach (var file in files)
            {
                try { File.Delete(file); } catch { }
            }
        }

        [Test]
        public async Task Test_ConnectToServerWritePdfAsync()
        {
            DataTable dt = GetTable();
            var reportData = new Common.Data()
            {
                DataTables = new Dictionary<string, string>(),
                MoveObjectPosition = new List<Common.MoveObjects>(),
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

            await CreatePdfFromReport("the_dotnet_dataset_report.rpt", "the_dataset_report1.pdf", reportData);
            await CreatePdfFromReport("the_dotnet_dataset_report.rpt", "the_dataset_report_ienumerable1.pdf", reportDataList);
            await CreatePdfFromReport("thereport.rpt", "thereport1.pdf", new Data());

            var parameterReportData = new Data();
            parameterReportData.Parameters.Add("MyParameter", "My First Parameter");
            parameterReportData.Parameters.Add("MyParameter2", true);

            await CreatePdfFromReport("thereport_wth_parameters.rpt", "thereport_wth_parameters1.pdf", parameterReportData);

            var subreportParameterData = new Data();
            subreportParameterData.SubReportParameters.Add(new SubReportParameters()
            {
                Parameters = parameterReportData.Parameters,
                ReportName = "thereport_wth_parameters.rpt"
            });
            await CreatePdfFromReport("thereport_with_subreport_with_parameters.rpt", "thereport_with_subreport_with_parameters1.pdf", subreportParameterData);

            var subreportDatatableData = new Data();
            subreportDatatableData.AddData("the_dotnet_dataset_report.rpt", "Employee", dt);
            await CreatePdfFromReport("thereport_with_subreport_with_dotnet_dataset.rpt", "thereport_with_subreport_with_dotnet_dataset1.pdf", subreportDatatableData);

            var fullData = new Data();
            fullData.AddData("EMPLOYEE", dt);
            fullData.AddData("the_dotnet_dataset_report_with_params", "Employee", dt);
            fullData.Parameters = parameterReportData.Parameters;
            fullData.SubReportParameters.Add(new SubReportParameters()
            {
                Parameters = parameterReportData.Parameters,
                ReportName = "the_dotnet_dataset_report_with_params"
            });
            await CreatePdfFromReport("the_dotnet_dataset_report_with_params_and_subreport.rpt", "the_dotnet_dataset_report_with_params_and_subreport1.pdf", fullData);

            var emptySubreportData = new Data();
            emptySubreportData.SetEmptyTable("EMPLOYEE");
            emptySubreportData.SetEmptyTable("the_dotnet_dataset_report_with_params", "Employee");
            emptySubreportData.Parameters = parameterReportData.Parameters;
            emptySubreportData.SubReportParameters.Add(new SubReportParameters()
            {
                Parameters = parameterReportData.Parameters,
                ReportName = "the_dotnet_dataset_report_with_params"
            });
            await CreatePdfFromReport("the_dotnet_dataset_report_with_params_and_subreport.rpt", "report_with_empty_subreport_datatable1.pdf", emptySubreportData);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists("the_dataset_report1.pdf"));
                Assert.That(new FileInfo("the_dataset_report1.pdf").Length > 0);
                Assert.That(File.Exists("the_dataset_report_ienumerable1.pdf"));
                Assert.That(new FileInfo("the_dataset_report_ienumerable1.pdf").Length > 0);
                Assert.That(File.Exists("thereport_wth_parameters1.pdf"));
                Assert.That(new FileInfo("thereport_wth_parameters1.pdf").Length > 0);
                Assert.That(File.Exists("thereport_with_subreport_with_parameters1.pdf"));
                Assert.That(new FileInfo("thereport_with_subreport_with_parameters1.pdf").Length > 0);
                Assert.That(File.Exists("thereport_with_subreport_with_dotnet_dataset1.pdf"));
                Assert.That(new FileInfo("thereport_with_subreport_with_dotnet_dataset1.pdf").Length > 0);
                Assert.That(File.Exists("the_dotnet_dataset_report_with_params_and_subreport1.pdf"));
                Assert.That(new FileInfo("the_dotnet_dataset_report_with_params_and_subreport1.pdf").Length > 0);
                Assert.That(File.Exists("report_with_empty_subreport_datatable1.pdf"));
                Assert.That(new FileInfo("report_with_empty_subreport_datatable1.pdf").Length > 0);
            });
        }

        [Test]
        public async Task Test_ClientBackwardsCompat()
        {
            DataTable dt = GetTable();
            var reportData = new Common.Data()
            {
                DataTables = new Dictionary<string, string>(),
                MoveObjectPosition = new List<Common.MoveObjects>(),
                Parameters = new Dictionary<string, object>()
            };
            reportData.AddData("EMPLOYEE", dt);

            var list = GetList();
            var reportDataList = new Common.Data()
            {
                DataTables = new Dictionary<string, string>(),
                MoveObjectPosition = new List<MoveObjects>(),
                Parameters = new Dictionary<string, object>()
            };
            reportDataList.AddData("Employee", list);

            await CreatePdfFromReport("the_dotnet_dataset_report.rpt", "the_dataset_report.pdf", reportData);
            await CreatePdfFromReport("the_dotnet_dataset_report.rpt", "the_dataset_report_ienumerable.pdf", reportDataList);
            await CreatePdfFromReport("thereport.rpt", "thereport.pdf", new Data());

            var parameterReportData = new Common.Data();
            parameterReportData.Parameters.Add("MyParameter", "My First Parameter");
            parameterReportData.Parameters.Add("MyParameter2", true);

            await CreatePdfFromReport("thereport_wth_parameters.rpt", "thereport_wth_parameters.pdf", parameterReportData);

            var subreportParameterData = new Common.Data();
            subreportParameterData.SubReportParameters.Add(new SubReportParameters()
            {
                Parameters = parameterReportData.Parameters,
                ReportName = "thereport_wth_parameters.rpt"
            });
            await CreatePdfFromReport("thereport_with_subreport_with_parameters.rpt", "thereport_with_subreport_with_parameters.pdf", subreportParameterData);

            var subreportDatatableData = new Common.Data();
            subreportDatatableData.AddData("the_dotnet_dataset_report.rpt", "Employee", dt);
            await CreatePdfFromReport("thereport_with_subreport_with_dotnet_dataset.rpt", "thereport_with_subreport_with_dotnet_dataset.pdf", subreportDatatableData);

            var fullData = new Common.Data();
            fullData.AddData("EMPLOYEE", dt);
            fullData.AddData("the_dotnet_dataset_report_with_params", "Employee", dt);
            fullData.Parameters = parameterReportData.Parameters;
            fullData.SubReportParameters.Add(new SubReportParameters()
            {
                Parameters = parameterReportData.Parameters,
                ReportName = "the_dotnet_dataset_report_with_params"
            });
            await CreatePdfFromReport("the_dotnet_dataset_report_with_params_and_subreport.rpt", "the_dotnet_dataset_report_with_params_and_subreport.pdf", fullData);

            var emptySubreportData = new Common.Data();
            emptySubreportData.SetEmptyTable("EMPLOYEE");
            emptySubreportData.SetEmptyTable("the_dotnet_dataset_report_with_params", "Employee");
            emptySubreportData.Parameters = parameterReportData.Parameters;
            emptySubreportData.SubReportParameters.Add(new SubReportParameters()
            {
                Parameters = parameterReportData.Parameters,
                ReportName = "the_dotnet_dataset_report_with_params"
            });
            await CreatePdfFromReport("the_dotnet_dataset_report_with_params_and_subreport.rpt", "report_with_empty_subreport_datatable.pdf", emptySubreportData);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists("the_dataset_report.pdf"));
                Assert.That(new FileInfo("the_dataset_report.pdf").Length > 0);
                Assert.That(File.Exists("thereport.pdf"));
                Assert.That(new FileInfo("thereport.pdf").Length > 0);
                Assert.That(File.Exists("thereport_wth_parameters.pdf"));
                Assert.That(new FileInfo("thereport_wth_parameters.pdf").Length > 0);
                Assert.That(File.Exists("thereport_with_subreport_with_parameters.pdf"));
                Assert.That(new FileInfo("thereport_with_subreport_with_parameters.pdf").Length > 0);
                Assert.That(File.Exists("thereport_with_subreport_with_dotnet_dataset.pdf"));
                Assert.That(new FileInfo("thereport_with_subreport_with_dotnet_dataset.pdf").Length > 0);
                Assert.That(File.Exists("the_dotnet_dataset_report_with_params_and_subreport.pdf"));
                Assert.That(new FileInfo("the_dotnet_dataset_report_with_params_and_subreport.pdf").Length > 0);
                Assert.That(File.Exists("report_with_empty_subreport_datatable.pdf"));
                Assert.That(new FileInfo("report_with_empty_subreport_datatable.pdf").Length > 0);
            });
        }

        [Test]
        public async Task Test_ReportAnalyzer()
        {
            DataTable dt = GetTable();
            var reportData = new Common.Data()
            {
                DataTables = new Dictionary<string, string>(),
                MoveObjectPosition = new List<Common.MoveObjects>(),
                Parameters = new Dictionary<string, object>()
            };
            reportData.AddData("EMPLOYEE", dt);

            var list = GetList();
            var reportDataList = new Common.Data()
            {
                DataTables = new Dictionary<string, string>(),
                MoveObjectPosition = new List<MoveObjects>(),
                Parameters = new Dictionary<string, object>()
            };
            reportDataList.AddData("Employee", list);

            using (var httpClient = new HttpClient())
            using (var instream = new FileStream("the_dotnet_dataset_report.rpt", FileMode.Open, FileAccess.Read))
            {
                var rpt = new Majorsilence.CrystalCmd.Client.ReportAnalyzer(instream,
                    httpClient, username: ClientTest.username, password: ClientTest.password, serverUrl: ClientTest.baseUrl);

                var response = await rpt.Analyze(CancellationToken.None);
                Assert.That(response != null);
                Assert.That(response.DataTables.Any(p => string.Equals(p.DataTableName, "employee",
                    StringComparison.OrdinalIgnoreCase)));
                Assert.That(response.Parameters.Count, Is.EqualTo(0));
                Assert.That(response.ReportObjects.Count, Is.EqualTo(5));
            }
        }

        [Test]
        public async Task Test_GeneratePdf_WithJwt()
        {
            var token = CreateJwt();

            await CreatePdfFromReport("thereport.rpt", "thereport_jwt.pdf", new Data());
            Assert.That(File.Exists("thereport_jwt.pdf"));
            Assert.That(new FileInfo("thereport_jwt.pdf").Length > 0);
        }

        [Test]
        public async Task Test_Analyzer_WithJwt()
        {
            var token = CreateJwt();

            using (var httpClient = new HttpClient())
            using (var instream = new FileStream("the_dotnet_dataset_report.rpt", FileMode.Open, FileAccess.Read))
            {
                var rpt = new Majorsilence.CrystalCmd.Client.ReportAnalyzer(instream,
                    httpClient, bearerToken: token, serverUrl: ClientTest.baseUrl);

                var response = await rpt.Analyze(CancellationToken.None);
                Assert.That(response != null);
                Assert.That(response.DataTables.Any(p => string.Equals(p.DataTableName, "employee",
                    StringComparison.OrdinalIgnoreCase)));
            }
        }

        private string CreateJwt()
        {
            // Manual HS256 JWT generator to avoid external package dependencies.
            var header = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
            var payloadObj = new Dictionary<string, object>
            {
                ["name"] = "user",
                ["iss"] = "https://localhost/",
                ["exp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1800
            };
            var payload = System.Text.Json.JsonSerializer.Serialize(payloadObj);

            static string Base64UrlEncode(byte[] input)
            {
                return Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            }

            var headerBytes = Encoding.UTF8.GetBytes(header);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var headerEncoded = Base64UrlEncode(headerBytes);
            var payloadEncoded = Base64UrlEncode(payloadBytes);
            var unsignedToken = headerEncoded + "." + payloadEncoded;

            using (var sha = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(bearerTokenKey)))
            {
                var sig = sha.ComputeHash(Encoding.UTF8.GetBytes(unsignedToken));
                var sigEncoded = Base64UrlEncode(sig);
                return unsignedToken + "." + sigEncoded;
            }
        }

        private async Task CreatePdfFromReport(string reportPath, string pdfOutputPath, Data reportData)
        {
            using (var fstream = new FileStream(reportPath, FileMode.Open))
            using (var fstreamOut = new FileStream(pdfOutputPath, FileMode.OpenOrCreate | FileMode.Append))
            {
                var rpt = new Client.Report(ClientTest.baseUrl, username: ClientTest.username, password: ClientTest.password);
                using (var stream = await rpt.GenerateAsync(reportData, fstream))
                {
                    await stream.CopyToAsync(fstreamOut);
                }
            }
        }

        static DataTable GetTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("EMPLOYEE_ID", typeof(int));
            table.Columns.Add("LAST_NAME", typeof(string));
            table.Columns.Add("FIRST_NAME", typeof(string));
            table.Columns.Add("BIRTH_DATE", typeof(DateTime));
            table.Columns.Add("TestData", typeof(byte[]));

            table.Rows.Add(25, "Indocin, Hi there", "David", DateTime.Now, Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add(50, "Enebrel", "Sam", DateTime.Now, Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add(10, "Hydralazine", "Christoff", DateTime.Now, Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add(21, "Combivent", "Janet", DateTime.Now, Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add(100, "Dilantin", "Melanie", DateTime.Now, Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add(101, "Hello", "World", DateTime.Now, Encoding.UTF8.GetBytes("Hello world"));
            return table;
        }

        static IEnumerable<Employee> GetList()
        {
            var list = new List<Employee>();
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 25,
                FIRST_NAME = "David",
                LAST_NAME = "Indocin",
                TestData = Encoding.UTF8.GetBytes("Hello world")
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 50,
                FIRST_NAME = "Sam",
                LAST_NAME = "Enebrel",
                TestData = Encoding.UTF8.GetBytes("Hello world")
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 10,
                FIRST_NAME = "Christoff",
                LAST_NAME = "Hydralazine",
                TestData = Encoding.UTF8.GetBytes("Hello world")
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 21,
                FIRST_NAME = "Janet",
                LAST_NAME = "Combivent",
                TestData = Encoding.UTF8.GetBytes("Hello world")
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 100,
                FIRST_NAME = "Melanie",
                LAST_NAME = "Dilantin",
                TestData = Encoding.UTF8.GetBytes("Hello world")
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 101,
                FIRST_NAME = "Hello",
                LAST_NAME = "World",
                TestData = Encoding.UTF8.GetBytes("Hello world")
            });
            list.Add(new Employee()
            {
                BIRTH_DATE = DateTime.Now,
                EMPLOYEE_ID = 102,
                FIRST_NAME = "IEnumerable",
                LAST_NAME = "List",
                TestData = Encoding.UTF8.GetBytes("Hello world")
            });
            return list;
        }
    }
}
