using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    public class Report : IReport
    {
        readonly string serverUrl;
        readonly string userAgent;
        public Report(string serverUrl = "https://c.majorsilence.com/export",
            string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0")
        {
            this.serverUrl = serverUrl;
            this.userAgent = userAgent;
        }


        //public Stream Generate(Data data, string reportPath)
        //{
        //    throw new NotImplementedException();
        //}

        //public Stream Generate(Data data, byte[] report)
        //{
        //    throw new NotImplementedException();
        //}


        /// <summary>
        /// Call a server with a crystal report template and data and have a pdf report returned.
        /// An HttpClient is created disposed on every call to this method.  It is recommended
        /// to call the GenerateAsync method passing in an HttpClient.
        /// </summary>
        /// <param name="reportData"></param>
        /// <param name="report"></param>
        /// <returns></returns>
        /// <example>
        /// <code lang="cs">
        /// using (var fstream = new FileStream("thereport.rpt", FileMode.Open))
        /// using (var fstreamOut = new FileStream("thereport.pdf", FileMode.OpenOrCreate | FileMode.Append))
        /// {
        ///     var rpt = new Majorsilence.CrystalCmd.Client.Report();
        ///     using (var stream = await rpt.GenerateAsync(new Data(), fstream))
        ///     {
        ///         stream.CopyTo(fstreamOut);
        ///     }
        /// }
        ///</code>
        /// </example>
        public async Task<Stream> GenerateAsync(Data reportData, Stream report)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(reportData);

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
                using (var form = new MultipartFormDataContent())
                {
                    form.Add(new StringContent(json), "reportdata");
                    form.Add(new StreamContent(report), "reporttemplate", "report.rpt");
                    //form.Add(new ByteArrayContent(crystalReport), "reporttemplate", "the_dataset_report.rpt");
                    HttpResponseMessage response = await httpClient.PostAsync(serverUrl, form);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStreamAsync();
                }
            }
        }

        /// <summary>
        /// Call a server with a crystal report template and data and have a pdf report returned.
        /// Is the recommended method for HttpClient performance reasons.
        /// </summary>
        /// <param name="reportData"></param>
        /// <param name="report"></param>
        /// <param name="httpClient">Manage the HttpClient from the calling program.</param>
        /// <returns></returns>
        /// <example>
        /// <code lang="cs">
        /// using (var fstream = new FileStream("thereport.rpt", FileMode.Open))
        /// using (var fstreamOut = new FileStream("thereport.pdf", FileMode.OpenOrCreate | FileMode.Append))
        /// {
        ///     var rpt = new Majorsilence.CrystalCmd.Client.Report();
        ///     using (var stream = await rpt.GenerateAsync(new Data(), fstream, new HttpClient()))
        ///     {
        ///         stream.CopyTo(fstreamOut);
        ///     }
        /// }
        ///</code>
        /// </example>
        public async Task<Stream> GenerateAsync(Data reportData, Stream report, HttpClient httpClient)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(reportData);

            using (var form = new MultipartFormDataContent())
            {
                form.Add(new StringContent(json), "reportdata");
                form.Add(new StreamContent(report), "reporttemplate", "report.rpt");
                //form.Add(new ByteArrayContent(crystalReport), "reporttemplate", "the_dataset_report.rpt");
                HttpResponseMessage response = await httpClient.PostAsync(serverUrl, form);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStreamAsync();
            }
        }

    }
}
