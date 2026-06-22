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

        [Test]
        public void DuplicateColumnNames_CaseInsensitive_DoesNotThrow()
        {
            // Simulates the merged DataTable produced by Cheque Register and any
            // other report whose stored proc unions result sets with inconsistently
            // cased column aliases (INVOICE / Invoice / invoice).
            var dt = new DataTable("APCHQREGDATA");
            dt.Columns.Add("BANK", typeof(string));
            dt.Columns.Add("INVOICE", typeof(string));
            dt.Columns.Add("Invoice", typeof(string));   // case-insensitive dup
            dt.Columns.Add("invoice", typeof(string));   // case-insensitive dup
            dt.Columns.Add("CODENAME", typeof(string));
            dt.Columns.Add("codename", typeof(string));  // case-insensitive dup
            dt.Columns.Add("INVAMT", typeof(decimal));
            dt.Columns.Add("invamt", typeof(decimal));   // case-insensitive dup

            var row0 = dt.NewRow();
            row0["BANK"] = "BNK3"; row0["INVOICE"] = "AP-1"; row0["CODENAME"] = "AP"; row0["INVAMT"] = 100m;
            dt.Rows.Add(row0);

            var row1 = dt.NewRow();
            row1["BANK"] = "BNK3"; row1["Invoice"] = "AR-1"; row1["codename"] = "AR"; row1["INVAMT"] = 200m;
            dt.Rows.Add(row1);

            var row2 = dt.NewRow();
            row2["BANK"] = "BNK3"; row2["invoice"] = "GL-1"; row2["codename"] = "GL"; row2["invamt"] = 300m;
            dt.Rows.Add(row2);

            var reportData = new Common.Data()
            {
                DataTables = new Dictionary<string, string>(),
                MoveObjectPosition = new List<Common.MoveObjects>(),
                Parameters = new Dictionary<string, object>(),
                ExportAs = Common.ExportTypes.PDF
            };
            reportData.AddData("APCHQREGDATA", dt);

            // Before the fix: this throws ChoRecordConfigurationException with
            // "Duplicate field name(s) [Name: INVOICE,CODENAME,INVAMT] found."
            DataTable result = null;
            Assert.DoesNotThrow(() =>
            {
                result = CsvReader.CreateTableEtl(reportData.DataTables.First().Value);
            });

            // The first occurrence of each name is preserved as-is. This is the
            // column Crystal's field binder will resolve to, matching in-process
            // behavior.
            Assert.That(result.Columns.Contains("INVOICE"), Is.True, "first INVOICE kept");
            Assert.That(result.Columns.Contains("CODENAME"), Is.True, "first CODENAME kept");
            Assert.That(result.Columns.Contains("INVAMT"), Is.True, "first INVAMT kept");

            // The first row's INVOICE/CODENAME/INVAMT come through intact.
            var r0 = result.Rows[0];
            Assert.That(r0["INVOICE"], Is.EqualTo("AP-1"));
            Assert.That(r0["CODENAME"], Is.EqualTo("AP"));
            // INVAMT is decimal but CSV round-trips strings; depending on the
            // CsvReader's type handling this may be string "100" or decimal 100m.
            Assert.That(r0["INVAMT"].ToString(), Is.EqualTo("100"));

            // We get 8 columns total because the duplicates are auto-renamed
            // (INVOICE_2, INVOICE_3, CODENAME_2, INVAMT_2), not dropped. This is
            // safe: Crystal binds the first match and ignores extras.
            Assert.That(result.Columns.Count, Is.EqualTo(8));
        }

        [Test]
        public void CreateTableEtl_WithValidCsvFile_ReturnsExpectedData()
        {
            var csvPath = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "csv_files", "valid.csv");
            var csvContent = System.IO.File.ReadAllText(csvPath);

            var result = CsvReader.CreateTableEtl(csvContent);
            string CleanCell(object value) => value?.ToString().Trim('\\', '"').Trim() ?? string.Empty;

            Assert.That(result.Columns.Count, Is.EqualTo(7));
            Assert.That(result.Rows.Count, Is.EqualTo(5));
            Assert.That(result.Columns[0].ColumnName, Is.EqualTo("Last Name"));
            Assert.That(result.Columns[6].ColumnName, Is.EqualTo("PostCode"));

            Assert.That(CleanCell(result.Rows[3]["Last Name"]), Is.EqualTo("LName4"));
            Assert.That(CleanCell(result.Rows[3]["First Name"]), Is.EqualTo("FName4"));
            Assert.That(CleanCell(result.Rows[3]["PROV"]), Is.EqualTo("NL"));

            Assert.That(CleanCell(result.Rows[4]["Address2"]), Is.EqualTo("123 fake street"));
            Assert.That(CleanCell(result.Rows[4]["Address3"]), Is.EqualTo("fake city"));
            Assert.That(CleanCell(result.Rows[4]["PostCode"]), Is.EqualTo(string.Empty));
        }

        [Test]
        public void CreateTableEtl_WithValidDateTimeFormats_ParsesDateTimeValues()
        {
            var csvPath = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "csv_files", "valid_datetime_formats.csv");
            var csvContent = System.IO.File.ReadAllText(csvPath);

            var result = CsvReader.CreateTableEtl(csvContent);

            Assert.That(result.Columns["CreatedOn"].DataType, Is.EqualTo(typeof(DateTime)));
            Assert.That(result.Rows.Count, Is.EqualTo(6));
            Assert.That(((DateTime)result.Rows[0]["CreatedOn"]).Date, Is.EqualTo(new DateTime(2024, 1, 31)));
            Assert.That(((DateTime)result.Rows[1]["CreatedOn"]), Is.EqualTo(new DateTime(2024, 2, 1, 14, 30, 0)));
            Assert.That(((DateTime)result.Rows[2]["CreatedOn"]), Is.EqualTo(new DateTime(2024, 3, 1, 19, 45, 0)));
            Assert.That(((DateTime)result.Rows[4]["CreatedOn"]), Is.EqualTo(new DateTime(2024, 3, 4, 8, 9, 10)));
            Assert.That(((DateTime)result.Rows[5]["CreatedOn"]), Is.EqualTo(new DateTime(2026, 5, 27, 9, 6, 29)));
        }

        [Test]
        public void CreateTableEtl_WithInvalidDateTimeFormats_ThrowsFormatException()
        {
            var csvPath = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "csv_files", "invalid_datetime_formats.csv");
            var csvContent = System.IO.File.ReadAllText(csvPath);

            Assert.Throws<FormatException>(() => CsvReader.CreateTableEtl(csvContent));
        }

        [Test]
        public void CreateTableEtl_WithBase64ByteArray_ParsesBytes()
        {
            // Some producers serialise byte[] columns with Convert.ToBase64String()
            // rather than the BitConverter dash-hex format the official client emits.
            // Both must be tolerated; previously the Base64 form crashed the render
            // with a FormatException out of HexStringToByteArray.
            var expected = new byte[] { 0x31, 0x55, 0xA8, 0x30, 0x3C, 0xEA, 0x01, 0x38,
                0xE6, 0xFD, 0x46, 0x97, 0x8A, 0x85, 0x42, 0xAA };
            var base64 = Convert.ToBase64String(expected); // "MVaoMDzqATjm/UaXioVCqg=="

            var csv = "Id,Barcode\nInt32,Byte[]\n\"1\",\"" + base64 + "\"\n";

            var result = CsvReader.CreateTableEtl(csv);

            Assert.That(result.Columns["Barcode"].DataType, Is.EqualTo(typeof(byte[])));
            Assert.That(result.Rows.Count, Is.EqualTo(1));
            Assert.That(result.Rows[0]["Barcode"], Is.EqualTo(expected));
        }

        [Test]
        public void CreateTableEtl_WithDashHexByteArray_StillParsesBytes()
        {
            // The official client (Data.DataTable2Csv via BitConverter.ToString)
            // continues to work after the Base64 tolerance was added.
            var expected = System.Text.Encoding.UTF8.GetBytes("Hello world");
            var dashHex = BitConverter.ToString(expected); // "48-65-6C-6C-6F-..."

            var csv = "Id,Payload\nInt32,Byte[]\n\"1\",\"" + dashHex + "\"\n";

            var result = CsvReader.CreateTableEtl(csv);

            Assert.That(result.Rows[0]["Payload"], Is.EqualTo(expected));
        }

        [Test]
        public void CreateTableEtl_WithInvalidByteArray_ThrowsFormatException()
        {
            // Neither dash-hex nor Base64 -> surfaced as a clear FormatException
            // instead of an opaque crash.
            var csv = "Id,Payload\nInt32,Byte[]\n\"1\",\"not*valid*bytes\"\n";

            Assert.Throws<FormatException>(() => CsvReader.CreateTableEtl(csv));
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
