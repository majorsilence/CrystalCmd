using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    internal class ServerCaller
    {
        HttpClient _client;
        string _baseUrl;
        public ServerCaller(HttpClient client, string urlBase)
        {
            _client = client;
            _baseUrl = urlBase;
        }

        public async Task<Stream> GenerateAsync<T>(T requestData, Stream report,
            string endPoint, string username, string password,
            string userAgent, string bearerToken,
            System.Threading.CancellationToken cancellationToken = default)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(requestData);

            using (var form = new MultipartFormDataContent())
            {
                form.Add(new StringContent(json, System.Text.Encoding.UTF8, "application/json"), typeof(T).Name);
                form.Add(new StreamContent(report), "reporttemplate", "report.rpt");

                var request = new HttpRequestMessage()
                {
                    Content = form,
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(_baseUrl + endPoint)
                };

                if(!string.IsNullOrWhiteSpace(bearerToken))
                {
                    request.Headers.Add("Authorization", $"Bearer {bearerToken}");
                }
                else if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                {
                    request.Headers.Add("Authorization", $"Basic {Base64Encode($"{username}:{password}")}");
                }
                request.Headers.Add("User-Agent", userAgent);

                HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);

                var content = response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                string errorMessage = "";
                try
                {
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        using (var x = new StreamReader(await content))
                        {
                            errorMessage = x.ReadToEnd();
                        }
                    }
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException hrex)
                {
                    throw new HttpRequestException(errorMessage, hrex);
                }

                return await content;
            }
        }
        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}
