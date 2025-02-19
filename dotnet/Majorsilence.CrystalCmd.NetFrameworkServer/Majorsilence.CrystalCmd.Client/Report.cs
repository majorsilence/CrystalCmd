﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    public class Report : IReport
    {
        readonly string serverUrl;
        readonly string userAgent;
        readonly string username;
        readonly string password;
        readonly string bearerToken;

        [Obsolete("Use the constructor that takes a bearer token.")]
        public Report(string serverUrl = "https://c.majorsilence.com/export",
            string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0",
            string username = "", string password = "")
        {
            this.serverUrl = serverUrl;
            this.userAgent = userAgent;
            this.username = username;
            this.password = password;
        }

        public Report(string serverUrl = "https://c.majorsilence.com/export",
           string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0",
           string bearerToken = "")
        {
            this.serverUrl = serverUrl;
            this.userAgent = userAgent;
            this.bearerToken = bearerToken;
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
            using (var httpClient = new HttpClient())
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
        public async Task<Stream> GenerateAsync(Common.Data reportData, Stream report, HttpClient httpClient,
            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(reportData);

            using (var form = new MultipartFormDataContent())
            {
                form.Add(new StringContent(json, System.Text.Encoding.UTF8, "application/json"), "reportdata");
                form.Add(new StreamContent(report), "reporttemplate", "report.rpt");

                using (var request = new HttpRequestMessage())
                {
                    request.Content = form;
                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri(serverUrl);

                    if (!string.IsNullOrWhiteSpace(bearerToken))
                    {
                        request.Headers.Add("Authorization", $"Bearer {bearerToken}");
                    }
                    else if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                    {
                        request.Headers.Add("Authorization", $"Basic {Base64Encode($"{username}:{password}")}");
                    }

                    HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                    var content = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    string errorMessage = "";
                    try
                    {
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            using (var x = new StreamReader(content))
                            {
                                errorMessage = await x.ReadToEndAsync();
                            }
                        }
                        response.EnsureSuccessStatusCode();
                    }
                    catch (HttpRequestException hrex)
                    {
                        throw new HttpRequestException(errorMessage, hrex);
                    }

                    // copy stream to memory stream to avoid disposed problem in caller
                    var resultStream = new MemoryStream();
                    await content.CopyToAsync(resultStream);
                    resultStream.Position = 0;
                    return resultStream;
                }
            }
        }

        /// <summary>
        /// Post heartbeat using gzip compression.  Use this to drastically reduce bandwidth.
        /// </summary>
        /// <param name="dto"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async System.Threading.Tasks.Task<Stream> GenerateViaCompressedPostAsync(Common.Data reportData, Stream report, HttpClient httpClient,
            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
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
                    request.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));
 

                    request.RequestUri = new Uri(serverUrl);

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
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            using (var x = new StreamReader(content))
                            {
                                errorMessage = await x.ReadToEndAsync();
                            }
                        }
                        response.EnsureSuccessStatusCode();
                    }
                    catch (HttpRequestException hrex)
                    {
                        throw new HttpRequestException(errorMessage, hrex);
                    }

                    // copy stream to memory stream to avoid disposed problem in caller
                    var resultStream = new MemoryStream();
                    await content.CopyToAsync(resultStream);
                    resultStream.Position = 0;
                    return resultStream;
                }
            }
        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}
