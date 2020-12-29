using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;


namespace Majorsilence.CrystalCmd.NetFrameworkServer.Controllers
{
    public class ExportController : ApiController
    {

        [HttpPost]
        public async Task<HttpResponseMessage> Post()
        {
            if (!Request.Content.IsMimeMultipartContent())
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);

            var provider = new MultipartMemoryStreamProvider();
            await Request.Content.ReadAsMultipartAsync(provider);

            Client.Data reportData = null;
            byte[] reportTemplate = null;

            foreach (var file in provider.Contents)
            {
                string name = file.Headers.ContentDisposition.Name;
                if (string.Equals(name, "reportdata", StringComparison.CurrentCultureIgnoreCase))
                {
                    var buffer = await file.ReadAsByteArrayAsync();
                    reportData = Newtonsoft.Json.JsonConvert.DeserializeObject<Client.Data>(await file.ReadAsStringAsync());
                }
                else
                {
                    reportTemplate = await file.ReadAsByteArrayAsync();
                }
            }

            string reportPath = null;
            byte[] bytes = null;
            try
            {
                reportPath = System.IO.Path.GetTempFileName() + ".rpt";
                // System.IO.File.WriteAllBytes(reportPath, reportTemplate);
                // Using System.IO.File.WriteAllBytes randomly causes problems where the system still 
                // has the file open when crystal attempts to load it and crystal fails.
                using (var fstream = new FileStream(reportPath, FileMode.Create))
                {
                    fstream.Write(reportTemplate, 0, reportTemplate.Length);
                    fstream.Flush();
                    fstream.Close();
                }

                var exporter = new PdfExporter();
                bytes = exporter.exportReportToStream(reportPath, reportData);


            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                try
                {
                    System.IO.File.Delete(reportPath);
                }
                catch (Exception)
                {
                    // TODO: cleanup will happen later
                }
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

    }
}
