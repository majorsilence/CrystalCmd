using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    public class Report
    {
        readonly string serverUrl;
        public Report(string serverUrl= "https://c.majorsilence.com/export")
        {
            this.serverUrl = serverUrl;
        }


        public Stream Generate(Data data, string reportPath)
        {
            throw new NotImplementedException();
        }

        public Stream Generate(Data data, byte[] report)
        {
            throw new NotImplementedException();
        }

        public async Task<Stream> GenerateAsync(Data reportData, Stream report)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(reportData);

            HttpClient httpClient = new HttpClient();
            MultipartFormDataContent form = new MultipartFormDataContent();

            form.Add(new StringContent(json), "reportdata");
            form.Add(new StreamContent(report), "reporttemplate", "report.rpt"); 
            //form.Add(new ByteArrayContent(crystalReport), "reporttemplate", "the_dataset_report.rpt");
            HttpResponseMessage response = await httpClient.PostAsync(serverUrl, form);

            response.EnsureSuccessStatusCode();
            httpClient.Dispose();
            //var result = response.Content.ReadAsByteArrayAsync().Result;
            //string sd = response.Content.ReadAsStringAsync ().Result;
            //System.IO.File.WriteAllBytes("test_report_from_server.pdf", result);

            return await response.Content.ReadAsStreamAsync();

        }

    }
}
