using Majorsilence.CrystalCmd.Server.Common;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web;
using System;
using System.Linq;
using System.Collections.Specialized;

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

    public static bool AuthFailed()
    {
        string user = Settings.GetSetting("Username");
        string password = Settings.GetSetting("Password");
        string jwtKey = Settings.GetSetting("JwtKey");

        if (!string.IsNullOrWhiteSpace(jwtKey))
        {
            var token = CustomServerSecurity.GetBearerToken(HttpContext.Current.Request.Headers);
            if (!string.IsNullOrWhiteSpace(token) && TokenVerifier.VerifyToken(token, jwtKey))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
        {
            // auth required
            var basicAuth = CustomServerSecurity.GetUserNameAndPassword(HttpContext.Current.Request.Headers);
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
}
