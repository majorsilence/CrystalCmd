using Majorsilence.CrystalCmd.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    public class ReportAnalyzer
    {
        readonly HttpClient _client;
        Stream _report;
        readonly string _baseUrl;
        readonly string _username;
        readonly string _password;
        readonly string _userAgent;
        readonly string _bearerToken;
        public ReportAnalyzer(Stream report, HttpClient client,
            string username, string password, string serverUrl = "https://c.majorsilence.com",
            string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0")
        {
            _report = report;
            _client = client;
            _baseUrl = GetBaseUrl(serverUrl);
            _username = username;
            _password = password;
            _userAgent = userAgent;
        }

        public ReportAnalyzer(Stream report, HttpClient client,
            string bearerToken, string serverUrl = "https://c.majorsilence.com",
            string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0")
        {
            _report = report;
            _client = client;
            _baseUrl = GetBaseUrl(serverUrl);
            _bearerToken = bearerToken;
            _userAgent = userAgent;
        }

        private string GetBaseUrl(string serverUrl)
        {
            var uri = new Uri(serverUrl);
            return $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        }

        public async Task<FullReportAnalysis> FullAnalysis(CancellationToken cancellationToken = default)
        {
            return await FullAnalysis(cancellationToken, false);
        }

        public async Task<Common.FullReportAnalysisResponse> Analyze(CancellationToken cancellationToken = default)
        {
            return await Analyze(cancellationToken, false);
        }

        public async Task<FullReportAnalysis> FullAnalysis(CancellationToken cancellationToken, bool enablePolling)
        {
            Func<Common.FullReportAnalysisResponse.DataTableAnalysisDto, DataTableAnalysis> createDataTable = t => new DataTableAnalysis(t.DataTableName, t.ColumnNames);
            var dto = await ServerRequest<object, Common.FullReportAnalysisResponse>(null, "/analyzer", _report, cancellationToken, enablePolling);
            return new FullReportAnalysis(
                dto.Parameters,
                dto.DataTables.Select(createDataTable),
                dto.SubReports.Select(s => new FullSubReportAnalysis(s.SubreportName, s.Parameters, s.DataTables.Select(createDataTable))),
                dto.ReportObjects);
        }

        public async Task<Common.FullReportAnalysisResponse> Analyze(CancellationToken cancellationToken, bool enablePolling)
        {
            var dto = await ServerRequest<object, Common.FullReportAnalysisResponse>(null, "/analyzer", _report, cancellationToken, false);
            return dto;
        }

        private async Task<U> ServerRequest<T, U>(T reportRequest, string analyzerEndPoint, Stream report, CancellationToken cancellationToken, bool enablePolling)
        {
            using (var response = await GenerateAsync(reportRequest, report, analyzerEndPoint, enablePolling,
                cancellationToken))
            {
                using (var responseReader = new StreamReader(response))
                {
                    var responseString = await responseReader.ReadToEndAsync();
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<U>(responseString);
                }
            }
        }

        private async Task<Stream> GenerateAsync<T>(T requestData, Stream report,
            string endPoint, bool enablePolling = true,
            System.Threading.CancellationToken cancellationToken = default)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(requestData);

            HttpStatusCode uploadResponse;
            string uploadId;

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

                if (!string.IsNullOrWhiteSpace(_bearerToken))
                {
                    request.Headers.Add("Authorization", $"Bearer {_bearerToken}");
                }
                else if (!string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_password))
                {
                    request.Headers.Add("Authorization", $"Basic {Base64Encode($"{_username}:{_password}")}");
                }
                request.Headers.Add("User-Agent", _userAgent);

                HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (enablePolling)
                {
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

                    if (uploadResponse == HttpStatusCode.OK)
                    {
                        return await PollGet(uploadId, _client, cancellationToken);
                    }

                    throw new CrystalCmdException("Failed to upload and analyze report file");
                }
                else
                {
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
        }
        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        private async Task<Stream> PollGet(string id, HttpClient httpClient,
           System.Threading.CancellationToken cancellationToken)
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgent);
            int pollTimeoutInSeconds = 600;
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalSeconds < pollTimeoutInSeconds)
            {
                using (var request = new System.Net.Http.HttpRequestMessage())
                {
                    request.Method = new System.Net.Http.HttpMethod("GET");
                    request.Headers.Add("id", id);
                    request.RequestUri = new Uri($"{_baseUrl}/analyzer/poll");

                    if (!string.IsNullOrWhiteSpace(_bearerToken))
                    {
                        request.Headers.Add("Authorization", $"Bearer {_bearerToken}");
                    }
                    else if (!string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_password))
                    {
                        request.Headers.Add("Authorization", $"Basic {Base64Encode($"{_username}:{_password}")}");
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


    }
}
