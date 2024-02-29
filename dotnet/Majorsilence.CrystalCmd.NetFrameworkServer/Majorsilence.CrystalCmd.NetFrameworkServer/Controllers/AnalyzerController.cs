using Majorsilence.CrystalCmd.Server.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Majorsilence.CrystalCmd.Client;



namespace Majorsilence.CrystalCmd.NetFrameworkServer.Controllers
{
    public class AnalyzerController : ApiController
    {
        [HttpPost]
        public async Task<FullReportAnalysisResponse> FullAnalysis()
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

            return response;
        }
    }
}
