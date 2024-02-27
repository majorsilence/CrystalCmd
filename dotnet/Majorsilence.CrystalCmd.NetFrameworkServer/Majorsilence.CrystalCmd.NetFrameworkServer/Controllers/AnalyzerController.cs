using Majorsilence.CrystalCmd.Common;
using Majorsilence.CrystalCmd.Server.Common;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace Majorsilence.CrystalCmd.NetFrameworkServer.Controllers.Analyzer
{
    public class AnalyzerController : ApiController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> FullAnalysis()
        {
            var serverSetup = new ServerSetup();
            serverSetup.CheckAuthAndMimeType(Request);

            var (reportData, reportTemplate) = await serverSetup.GetTemplateAndData<FullReportAnalysisResponse>(Request);

            var analyzer = new CrystalReportsAnalyzer();
            FullReportAnalysisResponse response = null;
            serverSetup.CreateReportAndGetPath(
                reportTemplate,
                reportPath => response = analyzer.GetFullAnalysis(reportPath)
            );

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(response))
            };
        }
    }
}
