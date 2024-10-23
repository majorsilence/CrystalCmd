using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Majorsilence.CrystalCmd.Server.Common;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Tests
{
    [TestFixture]
    public class AnalyzerTest
    {
        [Test]
        public void BasicTest()
        {
            var analyzer = new CrystalReportsAnalyzer();
            var response = analyzer.GetFullAnalysis("analyzer_report.rpt");

            Assert.That(response != null);
            Assert.That(response.DataTables.Any(p => string.Equals(p.DataTableName, "employee",
                StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.Parameters.Any(p => string.Equals(p, "MyParameter",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.Parameters.Any(p => string.Equals(p, "MyParameter2",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ParametersExtended.Any(p => string.Equals(p.Key, "MyParameter",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ParametersExtended.Any(p => string.Equals(p.Value, "StringParameter",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ParametersExtended.Any(p => string.Equals(p.Key, "MyParameter2",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ParametersExtended.Any(p => string.Equals(p.Value, "BooleanParameter",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectName, "Text",
                StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectValue, "Employee List",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => p.TopPosition == 122));
            Assert.That(response.ReportObjects.Any(p => p.Width == 4405));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectName, "Text1",
                StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectValue, "Employee_Id",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => p.TopPosition == 376));
            Assert.That(response.ReportObjects.Any(p => p.Width == 1408));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectName, "Text2",
                StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectValue, "Last_Name",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => p.TopPosition == 376));
            Assert.That(response.ReportObjects.Any(p => p.Width == 2584));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectName, "EmployeeId1",
                StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectValue, "CrystalDecisions.CrystalReports.Engine.FieldObject",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => p.TopPosition == 0));
            Assert.That(response.ReportObjects.Any(p => p.Width == 1408));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectName, "LastName1",
                StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectValue, "CrystalDecisions.CrystalReports.Engine.FieldObject",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => p.TopPosition == 0));
            Assert.That(response.ReportObjects.Any(p => p.Width == 2584));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectName, "Subreport1",
                StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectValue, "CrystalDecisions.CrystalReports.Engine.FieldObject",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => p.TopPosition == 680));
            Assert.That(response.ReportObjects.Any(p => p.Width == 3896));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectName, "MyParameter1",
                StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectValue, "CrystalDecisions.CrystalReports.Engine.FieldObject",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => p.TopPosition == 120));
            Assert.That(response.ReportObjects.Any(p => p.Width == 2584));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectName, "MyParameter21",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => string.Equals(p.ObjectValue, "CrystalDecisions.CrystalReports.Engine.FieldObject",
               StringComparison.OrdinalIgnoreCase)));
            Assert.That(response.ReportObjects.Any(p => p.TopPosition == 480));
            Assert.That(response.ReportObjects.Any(p => p.Width == 2584));

            Assert.That(response.SubReports.Any(p => string.Equals(p.SubreportName, "the_dotnet_dataset_report_with_params",
                StringComparison.OrdinalIgnoreCase)));
        }
    }
}
