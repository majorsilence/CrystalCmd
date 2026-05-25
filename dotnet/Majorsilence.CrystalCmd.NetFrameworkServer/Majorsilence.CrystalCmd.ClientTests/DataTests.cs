using Majorsilence.CrystalCmd.Common;
using NUnit.Framework;
using System.Data;

namespace Majorsilence.CrystalCmd.ClientTests
{
    [TestFixture]
    public class DataTests
    {
        private const string ChequesKey = "cheques";
        private const string EmployeesKey = "employees";
        private const string VendorColumn = "VENDOR";
        private const string CsvColumn = "col1";

        private const string VendorA = "VENDOR_A";
        private const string VendorB = "VENDOR_B";
        private const string CsvValue1 = "val1";
        private const string CsvValue2 = "val2";
        private const string Alice = "Alice";
        private const string Bob = "Bob";

        private static DataTable CreateTable(string columnName, params string[] values)
        {
            var dt = new DataTable();
            dt.Columns.Add(columnName, typeof(string));
            foreach (var v in values)
                dt.Rows.Add(v);
            return dt;
        }

        [Test]
        public void AddData_DataTable_SameKeyTwice_DoesNotThrow()
        {
            var data = new Data();
            var dt1 = CreateTable(VendorColumn, VendorA);
            var dt2 = CreateTable(VendorColumn, VendorB);

            data.AddData(ChequesKey, dt1);

            Assert.DoesNotThrow(() => data.AddData(ChequesKey, dt2));
        }

        [Test]
        public void AddData_DataTable_SameKeyTwice_OverwritesData()
        {
            var data = new Data();
            var dt1 = CreateTable(VendorColumn, VendorA);
            var dt2 = CreateTable(VendorColumn, VendorB);

            data.AddData(ChequesKey, dt1);
            data.AddData(ChequesKey, dt2);

            Assert.Multiple(() =>
            {
                Assert.That(data.DataTables[ChequesKey], Does.Contain(VendorB));
                Assert.That(data.DataTables[ChequesKey], Does.Not.Contain(VendorA));
            });
        }

        [Test]
        public void AddData_IEnumerable_SameKeyTwice_DoesNotThrow()
        {
            var data = new Data();
            var list1 = new[] { new Employee { FIRST_NAME = Alice } };
            var list2 = new[] { new Employee { FIRST_NAME = Bob } };

            data.AddData(EmployeesKey, list1);

            Assert.DoesNotThrow(() => data.AddData(EmployeesKey, list2));

            Assert.Multiple(() =>
            {
                Assert.That(data.DataTables[EmployeesKey], Does.Contain(Bob));
                Assert.That(data.DataTables[EmployeesKey], Does.Not.Contain(Alice));
            });
        }

        [Test]
        public void AddData_RawCsv_SameKeyTwice_DoesNotThrow()
        {
            var data = new Data();
            var csv1 = $"{CsvColumn}\r\n{CsvValue1}";
            var csv2 = $"{CsvColumn}\r\n{CsvValue2}";

            data.AddData(ChequesKey, csv1);

            Assert.DoesNotThrow(() => data.AddData(ChequesKey, csv2));
        }

        [Test]
        public void AddData_RawCsv_SameKeyTwice_OverwritesData()
        {
            var data = new Data();
            var csv1 = $"{CsvColumn}\r\n{CsvValue1}";
            var csv2 = $"{CsvColumn}\r\n{CsvValue2}";

            data.AddData(ChequesKey, csv1);
            data.AddData(ChequesKey, csv2);

            Assert.That(data.DataTables[ChequesKey], Is.EqualTo(csv2));
        }
    }
}