using System.Net.Http;
using System.Net;
using System.Web.Http;
using Majorsilence.CrystalCmd.Server.Common;
using System.Web;
using System;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace Majorsilence.CrystalCmd.NetFrameworkServer
{
    public class ServerSetup
    {
        public void CheckAuthAndMimeType(HttpRequestMessage request)
        {
            if (!request.Content.IsMimeMultipartContent())
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);

            if (AuthFailed())
            {
                throw new HttpResponseException(HttpStatusCode.Unauthorized);
            }
        }

        public async Task<(T data, byte[] template)> GetTemplateAndData<T>(HttpRequestMessage request)
        {
            var provider = new MultipartMemoryStreamProvider();
            await request.Content.ReadAsMultipartAsync(provider);

            (T data, byte[] template) report = (default, null);

            foreach (var file in provider.Contents)
            {
                string name = file.Headers.ContentDisposition.Name.Replace("\"", "");
                if (string.Equals(name, typeof(T).Name, StringComparison.CurrentCultureIgnoreCase))
                {
                    report.data = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(await file.ReadAsStringAsync());
                }
                else
                {
                    report.template = await file.ReadAsByteArrayAsync();
                }
            }

            return report;
        }

        public void CreateReportAndGetPath(byte[] reportTemplate, Action<string> usePath)
        {
            string reportPath = Path.Combine(WorkingFolder.GetMajorsilenceTempFolder(), $"{Guid.NewGuid().ToString()}.rpt");
            try
            {
                // System.IO.File.WriteAllBytes(reportPath, reportTemplate);
                // Using System.IO.File.WriteAllBytes randomly causes problems where the system still 
                // has the file open when crystal attempts to load it and crystal fails.
                using (var fstream = new FileStream(reportPath, FileMode.Create))
                {
                    fstream.Write(reportTemplate, 0, reportTemplate.Length);
                    fstream.Flush();
                    fstream.Close();
                }

                usePath(reportPath);
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