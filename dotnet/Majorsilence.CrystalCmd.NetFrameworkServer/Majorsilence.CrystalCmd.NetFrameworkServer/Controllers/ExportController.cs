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
            if (!Request.Content.IsMimeMultipartContent())
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);

            if (AuthFailed())
            {
                var authproblem = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                return authproblem;
            }

            var provider = new MultipartMemoryStreamProvider();
            await Request.Content.ReadAsMultipartAsync(provider);

            CrystalCmd.Common.Data reportData = null;
            byte[] reportTemplate = null;

            foreach (var file in provider.Contents)
            {
                string name = file.Headers.ContentDisposition.Name.Replace("\"", "");
                if (string.Equals(name, "reportdata", StringComparison.CurrentCultureIgnoreCase))
                {
                    reportData = Newtonsoft.Json.JsonConvert.DeserializeObject<CrystalCmd.Common.Data>(await file.ReadAsStringAsync());
                }
                else
                {
                    reportTemplate = await file.ReadAsByteArrayAsync();
                }
            }

            string reportPath = null;
            byte[] bytes = null;
            string fileExt = "pdf";
            string mimeType = "application/octet-stream";
            try
            {
                string workingDir = WorkingFolder.GetMajorsilenceTempFolder();
                reportPath = Path.Combine(workingDir, $"{Guid.NewGuid().ToString()}.rpt");
                if (!Directory.Exists(workingDir))
                {
                    Directory.CreateDirectory(workingDir);
                }
                // System.IO.File.WriteAllBytes(reportPath, reportTemplate);
                // Using System.IO.File.WriteAllBytes randomly causes problems where the system still 
                // has the file open when crystal attempts to load it and crystal fails.
                using (var fstream = new FileStream(reportPath, FileMode.Create))
                {
                    fstream.Write(reportTemplate, 0, reportTemplate.Length);
                    fstream.Flush();
                    fstream.Close();
                }

                var exporter = new Majorsilence.CrystalCmd.Server.Common.Exporter();
                var output = exporter.exportReportToStream(reportPath, reportData);
                bytes = output.Item1;
                fileExt = output.Item2;
                mimeType = output.Item3;
            }
            catch (Exception ex)
            {
                var message = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(ex.Message + System.Environment.NewLine + ex.StackTrace)
                };

                return message;
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
                FileName = $"report.{fileExt}"
            };
            result.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            return result;

        }

        private static bool AuthFailed()
        {
            string user = Settings.GetSetting("Username");
            string password = Settings.GetSetting("Password");
            if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
            {
                // auth required
                var basicAuth = GetUserNameAndPassword(HttpContext.Current);
                if (!basicAuth.HasValue)
                {
                    //Auth problem
                    return true;
                }
                if (!string.Equals(user, basicAuth.Value.UserName, StringComparison.InvariantCultureIgnoreCase) ||
                    !string.Equals(password, basicAuth.Value.Password, StringComparison.InvariantCulture))
                {
                    // auth problem
                    return true;
                }
            }

            return false;
        }

        private static (string UserName, string Password)? GetUserNameAndPassword(HttpContext context)
        {
            var auth = context.Request.Headers.GetValues("Authorization")?.FirstOrDefault().Replace("Basic ", "");
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(auth ?? ""));
            if (string.IsNullOrWhiteSpace(credentials))
            {
                return null;
            }

            int separator = credentials.IndexOf(':');
            string name = credentials.Substring(0, separator);
            string password = credentials.Substring(separator + 1);

            return (name, password);
        }

    }
}
