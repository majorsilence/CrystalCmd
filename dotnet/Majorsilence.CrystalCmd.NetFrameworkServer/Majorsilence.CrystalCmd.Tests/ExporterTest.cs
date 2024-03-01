using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using NUnit.Framework;
using System;
using System.Text;

namespace Majorsilence.CrystalCmd.Tests
{
    [TestFixture]
    public class ExporterTest
    {

        [TestCase(Client.ExportTypes.PDF, "application/pdf", "pdf")]
        [TestCase(Client.ExportTypes.WordDoc, "application/msword", "doc")]
        [TestCase(Client.ExportTypes.ExcelDataOnly, "application/vnd.ms-excel", "xls")]
        [TestCase(Client.ExportTypes.Excel, "application/vnd.ms-excel", "xls")]
        [TestCase(Client.ExportTypes.CSV, "text/csv", "csv")]
        [TestCase(Client.ExportTypes.RichText, "application/rtf", "rtf")]
        [TestCase(Client.ExportTypes.TEXT, "text/plain", "txt")]
        public void ExportTest(Client.ExportTypes exportType, string expectedMimeType, string expectedExtension)
        {
            var export = new Majorsilence.CrystalCmd.Server.Common.Exporter();
            var result = export.exportReportToStream("thereport.rpt", new Client.Data()
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

            if (exportType == Client.ExportTypes.PDF)
            {
                var text = ExtractTextFromPdf(bytes);
                Assert.That(text.Contains("Test Report"));
            }
            if(exportType == Client.ExportTypes.CSV || exportType == Client.ExportTypes.TEXT)
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
