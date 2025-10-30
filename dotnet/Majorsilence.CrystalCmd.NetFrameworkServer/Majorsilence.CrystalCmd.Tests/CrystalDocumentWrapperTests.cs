using CrystalDecisions.CrystalReports.Engine;
using Majorsilence.CrystalCmd.Server.Common;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing.Imaging;
using System.Text;

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
                Assert.Multiple(() =>
                {
                    Assert.That(reportClientDocument.ReportDefinition.ReportObjects[objectName].Top, Is.EqualTo(finalPosition));
                });
            }

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
            Assert.DoesNotThrow(() =>
            {
                using (var reportClientDocument = crystalWrapper.Create("thereport.rpt", dto.ReportData))
                {
                }
            });
        }

    }
}
