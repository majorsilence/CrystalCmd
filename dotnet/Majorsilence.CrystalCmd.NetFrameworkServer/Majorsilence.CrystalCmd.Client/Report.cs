using Majorsilence.CrystalCmd.Common;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    public class Report : IReport
    {
        readonly string baseUrl;
        readonly string userAgent;
        readonly string username;
        readonly string password;
        readonly string bearerToken;

        [Obsolete("Use the constructor that takes a bearer token.")]
        public Report(string serverUrl = "https://c.majorsilence.com",
            string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0",
            string username = "", string password = "")
        {
            this.baseUrl = GetBaseUrl(serverUrl);
            this.userAgent = userAgent;
            this.username = username;
            this.password = password;
        }

        public Report(string serverUrl = "https://c.majorsilence.com",
           string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0",
           string bearerToken = "")
        {
            this.baseUrl = GetBaseUrl(serverUrl);
            this.userAgent = userAgent;
            this.bearerToken = bearerToken;
        }

        private string GetBaseUrl(string serverUrl)
        {
            var uri = new Uri(serverUrl);
            return $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        }


        public Stream Generate(Common.Data reportData, Stream report)
        {
            return System.Threading.Tasks.Task.Run(async () => await GenerateAsync(reportData, report,
                System.Threading.CancellationToken.None)).GetAwaiter().GetResult();
        }

        public Stream Generate(Common.Data reportData, Stream report, HttpClient httpClient)
        {
            return System.Threading.Tasks.Task.Run(async () => await GenerateAsync(reportData, report, httpClient,
                System.Threading.CancellationToken.None)).GetAwaiter().GetResult();
        }

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
        public async Task<Stream> GenerateAsync(Common.Data reportData, Stream report,
            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = cookieContainer
            };
            using (var httpClient = new HttpClient(handler))
            {
                return await GenerateAsync(reportData, report, httpClient, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Call a server with a crystal report template and data and have a pdf report returned.
        /// Is the recommended method for HttpClient performance reasons.
        /// </summary>
        /// <param name="reportData"></param>
        /// <param name="report"></param>
        /// <param name="httpClient">Manage the HttpClient from the calling program.  
        /// It is imperative that if load balancing is in place using session affinity, 
        /// aka sticky sessions, a CookiContainer and handler should be used</param>
        /// <returns></returns>
        /// <example>
        /// <code lang="cs">
        /// var cookieContainer = new CookieContainer();
        /// var handler = new HttpClientHandler
        /// {
        ///  CookieContainer = cookieContainer
        /// };
        /// using (var fstream = new FileStream("thereport.rpt", FileMode.Open))
        /// using (var fstreamOut = new FileStream("thereport.pdf", FileMode.OpenOrCreate | FileMode.Append))
        /// {
        ///     var rpt = new Majorsilence.CrystalCmd.Client.Report();
        ///     using (var stream = await rpt.GenerateAsync(new Data(), fstream, new HttpClient(handler)))
        ///     {
        ///         stream.CopyTo(fstreamOut);
        ///     }
        /// }
        ///</code>
        /// </example>
        public async Task<Stream> GenerateAsync(Common.Data reportData, Stream report, HttpClient httpClient,
            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

            HttpStatusCode uploadResponse;
            string uploadId;
            using (var ms = new MemoryStream())
            {
                await report.CopyToAsync(ms);
                var streamingValue = new Common.StreamedRequest()
                {
                    ReportData = reportData,
                    Template = ms.ToArray()
                };

#if NET6_0_OR_GREATER
                // HACK:  This is a hack to get around the fact that the CompressedContent class does not seem to work in .NET 6.0 and newer
                var compressed = CompressedContent.Compress(Newtonsoft.Json.JsonConvert.SerializeObject(streamingValue));
#endif
                using (var request = new System.Net.Http.HttpRequestMessage())
#if NET6_0_OR_GREATER
                using (var inputContent = new System.Net.Http.StreamContent(compressed))
#else
                using (var inputContent = new System.Net.Http.StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(streamingValue)))
                using (var compressedContent = new CompressedContent(inputContent, "gzip"))
#endif
                {
                    inputContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
                    inputContent.Headers.ContentEncoding.Add("gzip");
#if NET6_0_OR_GREATER
                    request.Content = inputContent;
#else
                    request.Content = compressedContent;
#endif
                    request.Method = new System.Net.Http.HttpMethod("POST");
                    request.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("text/plain; charset=utf-8"));
                    request.RequestUri = new Uri($"{baseUrl}/export/poll");

                    if (!string.IsNullOrWhiteSpace(bearerToken))
                    {
                        request.Headers.Add("Authorization", $"Bearer {bearerToken}");
                    }
                    else if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                    {
                        request.Headers.Add("Authorization", $"Basic {Base64Encode($"{username}:{password}")}");
                    }

                    var response = await httpClient.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    uploadId = content;
                    uploadResponse = response.StatusCode;
                    string errorMessage = "";
                    try
                    {
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            errorMessage = content;
                        }
                        response.EnsureSuccessStatusCode();
                    }
                    catch (HttpRequestException hrex)
                    {
                        throw new HttpRequestException(errorMessage, hrex);
                    }
                }
            }

            if (uploadResponse == HttpStatusCode.OK)
            {
                return await PollGet(uploadId, httpClient, cancellationToken);
            }

            throw new CrystalCmdException("Failed to upload report file");
        }

        private async Task<Stream> PollGet(string id, HttpClient httpClient,
           System.Threading.CancellationToken cancellationToken)
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            int pollTimeoutInSeconds = 600;
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalSeconds < pollTimeoutInSeconds)
            {
                using (var request = new System.Net.Http.HttpRequestMessage())
                {
                    request.Method = new System.Net.Http.HttpMethod("GET");
                    request.Headers.Add("id", id);
                    request.RequestUri = new Uri($"{baseUrl}/export/poll");

                    if (!string.IsNullOrWhiteSpace(bearerToken))
                    {
                        request.Headers.Add("Authorization", $"Bearer {bearerToken}");
                    }
                    else if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                    {
                        request.Headers.Add("Authorization", $"Basic {Base64Encode($"{username}:{password}")}");
                    }

                    var response = await httpClient.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                    var content = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    string errorMessage = "";
                    try
                    {
                        if ((int)response.StatusCode > 299 || (int)response.StatusCode < 200)
                        {
                            using (var x = new StreamReader(content))
                            {
                                errorMessage = await x.ReadToEndAsync();
                            }
                            response.EnsureSuccessStatusCode();
                        }
                        else if (response.StatusCode == HttpStatusCode.Accepted)
                        {
                            // still processing, do nothing  
                        }
                        else if (response.StatusCode == HttpStatusCode.OK)
                        {
                            // processing finished, copy stream to memory stream to avoid disposed problem in caller
                            var resultStream = new MemoryStream();
                            await content.CopyToAsync(resultStream);
                            resultStream.Position = 0;
                            return resultStream;
                        }
                    }
                    catch (HttpRequestException hrex)
                    {
                        throw new HttpRequestException(errorMessage, hrex);
                    }
                }
                await Task.Delay(1000, cancellationToken);
            }

            throw new CrystalCmdException("Polling timed out");
        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}
