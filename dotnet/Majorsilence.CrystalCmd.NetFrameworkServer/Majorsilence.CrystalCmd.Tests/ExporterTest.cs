using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Text;

namespace Majorsilence.CrystalCmd.Tests
{
    [TestFixture]
    public class ExporterTest
    {

        private readonly Mock<ILogger> _mockLogger;

        public ExporterTest()
        {
            // Set up the mock logger
            _mockLogger = new Mock<ILogger>();
        }


        [TestCase(CrystalCmd.Common.ExportTypes.PDF, "application/pdf", "pdf")]
        [TestCase(CrystalCmd.Common.ExportTypes.WordDoc, "application/msword", "doc")]
        [TestCase(CrystalCmd.Common.ExportTypes.ExcelDataOnly, "application/vnd.ms-excel", "xls")]
        [TestCase(CrystalCmd.Common.ExportTypes.Excel, "application/vnd.ms-excel", "xls")]
        [TestCase(CrystalCmd.Common.ExportTypes.CSV, "text/csv", "csv")]
        [TestCase(CrystalCmd.Common.ExportTypes.RichText, "application/rtf", "rtf")]
        [TestCase(CrystalCmd.Common.ExportTypes.TEXT, "text/plain", "txt")]
        public void ExportTest(CrystalCmd.Common.ExportTypes exportType, string expectedMimeType, string expectedExtension)
        {
        
            var export = new Majorsilence.CrystalCmd.Server.Common.Exporter(_mockLogger.Object);
            var result = export.exportReportToStream("thereport.rpt", new CrystalCmd.Common.Data()
            {
                ExportAs = exportType
            });
            Assert.That(result != null);
            byte[] bytes = result.Item1;
            string fileExt = result.Item2;
            string mimeType = result.Item3;
            Assert.That(bytes != null);
            Assert.That(fileExt, Is.EqualTo(expectedExtension));
            Assert.That(mimeType, Is.EqualTo(expectedMimeType));

            if (exportType == CrystalCmd.Common.ExportTypes.PDF)
            {
                var text = ExtractTextFromPdf(bytes);
                Assert.That(text.Contains("Test Report"));
            }
            if(exportType == CrystalCmd.Common.ExportTypes.CSV || exportType == CrystalCmd.Common.ExportTypes.TEXT)
            {
                Assert.That(UTF8Encoding.UTF8.GetString(bytes).Contains("Test Report"));
            }

        }

        private static string ExtractTextFromPdf(byte[] reportData)
        {
            PdfReader reader = new PdfReader(reportData);
            StringBuilder text = new StringBuilder();

            for (int i = 1; i <= reader.NumberOfPages; i++)
            {
                text.Append(PdfTextExtractor.GetTextFromPage(reader, i));
            }

            return text.ToString();
        }
    }
}
