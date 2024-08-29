using Majorsilence.CrystalCmd.Server.Common;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Tests
{
    [TestFixture]
    public class CsvReaderTests
    {

        private readonly Mock<ILogger> _mockLogger;

        public CsvReaderTests()
        {
            // Set up the mock logger
            _mockLogger = new Mock<ILogger>();

            string workingfolder = WorkingFolder.GetMajorsilenceTempFolder();
            if (!System.IO.Directory.Exists(workingfolder))
            {
                System.IO.Directory.CreateDirectory(workingfolder);
            }

        }

        [Test]
        public void ColumnNamesCanContainPeriods()
        {
            var export = new Majorsilence.CrystalCmd.Server.Common.Exporter(_mockLogger.Object);

            DataTable dt = GetTable();
            var reportData = new Common.Data()
            {
                DataTables = new Dictionary<string, string>(),
                MoveObjectPosition = new List<Common.MoveObjects>(),
                Parameters = new Dictionary<string, object>(),
                ExportAs = Common.ExportTypes.PDF
            };
            reportData.AddData("EMPLOYEE", dt);

            var dtFromCsv = CsvReader.CreateTableEtl(reportData.DataTables.FirstOrDefault().Value);
            Assert.That(dtFromCsv.Columns.Contains("Special.Column"));
            Assert.That(dtFromCsv.Columns.Contains("EMPLOYEE_ID"));
            Assert.That(dtFromCsv.Columns.Contains("LAST_NAME"));
            Assert.That(dtFromCsv.Columns.Contains("FIRST_NAME"));
            Assert.That(dtFromCsv.Columns.Contains("BIRTH_DATE"));
            Assert.That(dtFromCsv.Columns.Contains("TestData"));
            Assert.That(dtFromCsv.Columns.Contains("Special.Column.That.Is.Larger"));

            var row1 = dtFromCsv.Rows[0];
            Assert.That(row1["Special.Column"], Is.EqualTo("Test column name with period"));
            Assert.That(row1["EMPLOYEE_ID"], Is.EqualTo(25));
            Assert.That(row1["LAST_NAME"], Is.EqualTo("Indocin, Hi there"));
            Assert.That(row1["FIRST_NAME"], Is.EqualTo("David"));
            Assert.That(row1["BIRTH_DATE"], Is.EqualTo(new DateTime(1984, 6, 19)));
            Assert.That(row1["TestData"], Is.EqualTo(System.Text.Encoding.UTF8.GetBytes("Hello world")));
            Assert.That(row1["Special.Column.That.Is.Larger"], Is.EqualTo("bigger"));

        }

        static DataTable GetTable()
        {
            // Here we create a DataTable with four columns.
            DataTable table = new DataTable();
            table.Columns.Add("Special.Column", typeof(string));
            table.Columns.Add("EMPLOYEE_ID", typeof(int));
            table.Columns.Add("LAST_NAME", typeof(string));
            table.Columns.Add("Special.Column.That.Is.Larger", typeof(string));
            table.Columns.Add("FIRST_NAME", typeof(string));
            table.Columns.Add("BIRTH_DATE", typeof(DateTime));
            table.Columns.Add("TestData", typeof(byte[]));

            // Here we add five DataRows.
            table.Rows.Add("Test column name with period", 25, "Indocin, Hi there", "bigger", "David",
                new DateTime(1984, 6, 19), System.Text.Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add("Test column name with period", 50, "Enebrel", "bigger", "Sam",
                DateTime.Now, System.Text.Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add("Test column name with period", 10, "Hydralazine", "bigger", "Christoff",
                DateTime.Now, System.Text.Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add("Test column name with period", 21, "Combivent", "bigger", "Janet",
                DateTime.Now, System.Text.Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add("Test column name with period", 100, "Dilantin", "bigger", "Melanie",
                DateTime.Now, System.Text.Encoding.UTF8.GetBytes("Hello world"));
            table.Rows.Add("Test column name with period", 101, "Hello", "bigger", "World",
                DateTime.Now, System.Text.Encoding.UTF8.GetBytes("Hello world"));
            return table;
        }
    }
}
