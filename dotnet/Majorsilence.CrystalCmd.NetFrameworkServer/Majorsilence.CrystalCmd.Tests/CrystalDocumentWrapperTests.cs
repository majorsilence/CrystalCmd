using Majorsilence.CrystalCmd.Server.Common;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Majorsilence.CrystalCmd.Tests
{
    [TestFixture]
    public class CrystalDocumentWrapperTests
    {

        private readonly Mock<ILogger> _mockLogger;

        public CrystalDocumentWrapperTests()
        {
            // Set up the mock logger
            _mockLogger = new Mock<ILogger>();

            string workingfolder = WorkingFolder.GetMajorsilenceTempFolder();
            if (!System.IO.Directory.Exists(workingfolder))
            {
                System.IO.Directory.CreateDirectory(workingfolder);
            }

        }


        [TestCase(CrystalCmd.Common.MoveType.ABSOLUTE, Common.MovePosition.TOP, 10, 10, "Text1")]
        [TestCase(CrystalCmd.Common.MoveType.ABSOLUTE, Common.MovePosition.TOP, 25, 25, "Text1")]
        [TestCase(CrystalCmd.Common.MoveType.RELATIVE, Common.MovePosition.TOP, 0, 720, "Text1")]
        [TestCase(CrystalCmd.Common.MoveType.RELATIVE, Common.MovePosition.TOP, 10, 730, "Text1")]
        public void MoveTopTest(Common.MoveType mt, Common.MovePosition mp, int moveSize, int finalPosition, string objectName)
        {

            var crystalWrapper = new CrystalDocumentWrapper(_mockLogger.Object);
            using (var reportClientDocument = crystalWrapper.Create("thereport.rpt", new CrystalCmd.Common.Data()
            {
                ExportAs = Common.ExportTypes.PDF,
                MoveObjectPosition = new List<CrystalCmd.Common.MoveObjects>() { new Common.MoveObjects() {
                Move = moveSize,
                ObjectName = objectName,
                Pos=  mp,
                Type = mt
                } },
            }))
            {
                Assert.Multiple((Action)(() =>
                {
                    Assert.That(reportClientDocument.ReportDefinition.ReportObjects[objectName].Top, Is.EqualTo(finalPosition));
                }));
            }

        }

        // Parameter names from clients don't always match the report's casing; the
        // wrapper resolves them case-insensitively to the report's canonical field.
        [TestCase("MyParameter", "MyParameter2")]
        [TestCase("MYPARAMETER", "MYPARAMETER2")]
        [TestCase("myparameter", "myparameter2")]
        public void SetParameterValueIsCaseInsensitive(string stringParamName, string boolParamName)
        {
            var crystalWrapper = new CrystalDocumentWrapper(_mockLogger.Object);
            Assert.DoesNotThrow((Action)(() =>
            {
                using (var reportClientDocument = crystalWrapper.Create("thereport_wth_parameters.rpt", new CrystalCmd.Common.Data()
                {
                    ExportAs = Common.ExportTypes.PDF,
                    Parameters = new Dictionary<string, object>()
                    {
                        { stringParamName, "hello world" },
                        { boolParamName, true }
                    }
                }))
                {
                    var stringParam = reportClientDocument.ParameterFields["MyParameter"];
                    Assert.Multiple((Action)(() =>
                    {
                        Assert.That(stringParam.HasCurrentValue, Is.True);
                        Assert.That((reportClientDocument.ParameterFields["MyParameter"].CurrentValues[0]
                            as CrystalDecisions.Shared.ParameterDiscreteValue).Value, Is.EqualTo("hello world"));
                    }));
                }
            }));
        }

        // A malformed value for a parameter the report requires must fail the export
        // with an error naming the parameter, not render with a silent default.
        [Test]
        public void MalformedValueForRequiredParameterFailsReport()
        {
            var crystalWrapper = new CrystalDocumentWrapper(_mockLogger.Object);
            var ex = Assert.Throws<InvalidOperationException>((TestDelegate)(() =>
            {
                using (crystalWrapper.Create("thereport_wth_parameters.rpt", new CrystalCmd.Common.Data()
                {
                    ExportAs = Common.ExportTypes.PDF,
                    Parameters = new Dictionary<string, object>()
                    {
                        { "MyParameter", "hello world" },
                        { "MyParameter2", "not-a-bool" }
                    }
                }))
                {
                }
            }));
            Assert.That(ex.Message, Does.Contain("MyParameter2"));
        }

        // Omitting a parameter the report requires must fail the export with an error
        // naming the parameter (previously it was silently defaulted to blank).
        [Test]
        public void MissingRequiredParameterFailsReport()
        {
            var crystalWrapper = new CrystalDocumentWrapper(_mockLogger.Object);
            var ex = Assert.Throws<InvalidOperationException>((TestDelegate)(() =>
            {
                using (crystalWrapper.Create("thereport_wth_parameters.rpt", new CrystalCmd.Common.Data()
                {
                    ExportAs = Common.ExportTypes.PDF,
                    Parameters = new Dictionary<string, object>()
                    {
                        { "MyParameter", "hello world" }
                        // MyParameter2 deliberately omitted
                    }
                }))
                {
                }
            }));
            Assert.That(ex.Message, Does.Contain("MyParameter2"));
        }

        // Parameter names that don't exist in the report are logged and ignored;
        // they must not abort the export.
        [Test]
        public void UnknownParameterNameDoesNotAbortReport()
        {
            var crystalWrapper = new CrystalDocumentWrapper(_mockLogger.Object);
            Assert.DoesNotThrow((Action)(() =>
            {
                using (var reportClientDocument = crystalWrapper.Create("thereport_wth_parameters.rpt", new CrystalCmd.Common.Data()
                {
                    ExportAs = Common.ExportTypes.PDF,
                    Parameters = new Dictionary<string, object>()
                    {
                        { "NoSuchParameter", "whatever" },
                        { "MyParameter", "hello world" },
                        { "MyParameter2", true }
                    }
                }))
                {
                    Assert.That((reportClientDocument.ParameterFields["MyParameter"].CurrentValues[0]
                        as CrystalDecisions.Shared.ParameterDiscreteValue).Value, Is.EqualTo("hello world"));
                }
            }));
        }

        // References to nonexistent report objects/formulas, and a sort request against
        // a report that defines no sort fields, must be logged and skipped, not thrown.
        [Test]
        public void InvalidObjectAndSortKeysDoNotAbortReport()
        {
            var crystalWrapper = new CrystalDocumentWrapper(_mockLogger.Object);
            Assert.DoesNotThrow((Action)(() =>
            {
                using (var reportClientDocument = crystalWrapper.Create("thereport.rpt", new CrystalCmd.Common.Data()
                {
                    ExportAs = Common.ExportTypes.PDF,
                    FormulaFieldText = new Dictionary<string, string>() { { "NoSuchFormula", "'x'" } },
                    CanGrow = new Dictionary<string, bool>() { { "NoSuchObject", true } },
                    Resize = new Dictionary<string, int>() { { "NoSuchObject", 100 } },
                    SortByField = new Dictionary<string, string>() { { "NoSuchTable", "NoSuchField" } }
                }))
                {
                }
            }));
        }

        private static void WithCulture(string cultureName, Action action)
        {
            var original = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(cultureName);
            try
            {
                action();
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = original;
            }
        }

        // "1.5" must mean one-and-a-half no matter what locale the server runs under.
        // Before the invariant-first parse, a German-locale server read it as 15.
        [Test]
        public void ParseDecimalIsNotAffectedByServerLocale()
        {
            WithCulture("de-DE", (Action)(() =>
            {
                Assert.That(CrystalDocumentWrapper.ParseDecimal("1.5"), Is.EqualTo(1.5m));
            }));
            WithCulture("en-US", (Action)(() =>
            {
                Assert.That(CrystalDocumentWrapper.ParseDecimal("1.5"), Is.EqualTo(1.5m));
            }));
        }

        // Values only the server locale can interpret still work via the fallback,
        // so pre-existing locale-dependent callers are not broken.
        [Test]
        public void ParseDecimalFallsBackToServerLocale()
        {
            WithCulture("de-DE", (Action)(() =>
            {
                Assert.That(CrystalDocumentWrapper.ParseDecimal("1,5"), Is.EqualTo(1.5m));
            }));
            WithCulture("en-US", (Action)(() =>
            {
                Assert.That(CrystalDocumentWrapper.ParseDecimal("1,000.25"), Is.EqualTo(1000.25m));
            }));
        }

        [Test]
        public void ParseDateParameterValueIsNotAffectedByServerLocale()
        {
            WithCulture("de-DE", (Action)(() =>
            {
                Assert.That(CrystalDocumentWrapper.ParseDateParameterValue("2026-07-16T13:45:00"),
                    Is.EqualTo(new DateTime(2026, 7, 16, 13, 45, 0)));
                Assert.That(CrystalDocumentWrapper.ParseDateParameterValue(new DateTime(2026, 7, 16)),
                    Is.EqualTo(new DateTime(2026, 7, 16)));
            }));
        }

        [Test]
        public void ParseDateParameterValueFallsBackToServerLocale()
        {
            WithCulture("de-DE", (Action)(() =>
            {
                Assert.That(CrystalDocumentWrapper.ParseDateParameterValue("31.12.2026"),
                    Is.EqualTo(new DateTime(2026, 12, 31)));
            }));
        }

        class ReportDto
        {
            public Majorsilence.CrystalCmd.Common.Data ReportData { get; set; }
        }
        [Ignore("This is for manual testing only")]
        [Test]
        public void TestFromJson()
        {
            var dto = Newtonsoft.Json.JsonConvert.DeserializeObject<ReportDto>(System.IO.File.ReadAllText("C:\\Path\\To\\Data\\9d57fd6c-8d62-4066-be3f-387f6ae251db.json"));
            var crystalWrapper = new CrystalDocumentWrapper(_mockLogger.Object);
            Assert.DoesNotThrow((Action)(() =>
            {
                using (var reportClientDocument = crystalWrapper.Create("thereport.rpt", dto.ReportData))
                {
                }
            }));
        }

    }
}
