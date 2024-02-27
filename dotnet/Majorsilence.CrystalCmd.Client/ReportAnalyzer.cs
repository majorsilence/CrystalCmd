using Majorsilence.CrystalCmd.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    public class ReportAnalyzer
    {
        HttpClient _client;
        Stream _report;
        string _serverUrl;
        string _username;
        string _password;
        string _userAgent;
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

        public async Task<FullReportAnalysis> FullAnalysis(CancellationToken cancellationToken = default)
        {
            Func<FullReportAnalysisResponse.DataTableAnalysisDto, DataTableAnalysis> createDataTable = t => new DataTableAnalysis(t.DataTableName, t.ColumnNames);
            var dto = await ServerRequest<object, FullReportAnalysisResponse>(null, "/FullAnalysis", _report, cancellationToken);
            return new FullReportAnalysis(
                dto.Parameters, 
                dto.DataTables.Select(createDataTable), 
                dto.SubReports.Select(s => new FullSubReportAnalysis(s.SubreportName, s.Parameters, s.DataTables.Select(createDataTable)))
            );
        }

        private async Task<U> ServerRequest<T, U>(T reportRequest, string analyzerEndPoint, Stream report, CancellationToken cancellationToken)
        {
            var serverCaller = new ServerCaller(_client, _serverUrl);
            using (var response = await serverCaller.GenerateAsync(reportRequest, report, analyzerEndPoint,
                _username, _password, _userAgent, cancellationToken))
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
