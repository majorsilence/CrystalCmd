using Majorsilence.CrystalCmd.Client;
using Majorsilence.CrystalCmd.Common;
using Majorsilence.CrystalCmd.Server.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;


namespace Majorsilence.CrystalCmd.NetFrameworkServer.Controllers
{
    public class ExportController : ApiController
    {

        [HttpPost]
        public async Task<HttpResponseMessage> Post()
        {
            var serverSetup = new ServerSetup();
            serverSetup.CheckAuthAndMimeType(Request);

            var (reportData, reportTemplate) = await serverSetup.GetTemplateAndData<Data>(Request);

            byte[] bytes = null;
            try
            {
                serverSetup.CreateReportAndGetPath(
                    reportTemplate, 
                    reportPath => bytes = ExportReportToPdf(reportData, reportPath)
                );
            }
            catch (Exception ex)
            {
                var message = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(ex.Message + System.Environment.NewLine + ex.StackTrace)
                };

                return message;
            }

            var result = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            result.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
            {
                FileName = "report.pdf"
            };
            result.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return result;

        }

        private static byte[] ExportReportToPdf(Data reportData, string reportPath)
        {
            byte[] bytes;
            var exporter = new Majorsilence.CrystalCmd.Server.Common.PdfExporter();
            bytes = exporter.exportReportToStream(reportPath, reportData);
            return bytes;
        }
    }
}
