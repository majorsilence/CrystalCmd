using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace Majorsilence.CrystalCmd.Client
{
    public class ReportAnalyzer
    {
        readonly HttpClient _client;
        Stream _report;
        readonly string _serverUrl;
        readonly string _username;
        readonly string _password;
        readonly string _userAgent;
        readonly string _bearerToken;
        public ReportAnalyzer(Stream report, HttpClient client,
            string username, string password, string serverUrl = "https://c.majorsilence.com/analyzer",
            string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0")
        {
            _report = report;
            _client = client;
            _serverUrl = serverUrl;
            _username = username;
            _password = password;
            _userAgent = userAgent;
        }

        public ReportAnalyzer(Stream report, HttpClient client,
            string bearerToken, string serverUrl = "https://c.majorsilence.com/analyzer",
            string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0")
        {
            _report = report;
            _client = client;
            _serverUrl = serverUrl;
            _bearerToken = bearerToken;
            _userAgent = userAgent;
        }

        public async Task<FullReportAnalysis> FullAnalysis(CancellationToken cancellationToken = default)
        {
            Func<Common.FullReportAnalysisResponse.DataTableAnalysisDto, DataTableAnalysis> createDataTable = t => new DataTableAnalysis(t.DataTableName, t.ColumnNames);
            var dto = await ServerRequest<object, Common.FullReportAnalysisResponse>(null, "/analyzer", _report, cancellationToken);
            return new FullReportAnalysis(
                dto.Parameters,
                dto.DataTables.Select(createDataTable),
                dto.SubReports.Select(s => new FullSubReportAnalysis(s.SubreportName, s.Parameters, s.DataTables.Select(createDataTable))),
                dto.ReportObjects);
        }

        public async Task<Common.FullReportAnalysisResponse> Analyze(CancellationToken cancellationToken = default)
        {
            var dto = await ServerRequest<object, Common.FullReportAnalysisResponse>(null, "/analyzer", _report, cancellationToken);
            return dto;
        }

        private async Task<U> ServerRequest<T, U>(T reportRequest, string analyzerEndPoint, Stream report, CancellationToken cancellationToken)
        {
            var serverCaller = new ServerCaller(_client, _serverUrl);
            using (var response = await serverCaller.GenerateAsync(reportRequest, report, analyzerEndPoint,
                _username, _password, _userAgent, _bearerToken, cancellationToken))
            {
                using (var responseReader = new StreamReader(response))
                {
                    var responseString = await responseReader.ReadToEndAsync();
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<U>(responseString);
                }
            }
        }
    }
}
