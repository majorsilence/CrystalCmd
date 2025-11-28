using Majorsilence.CrystalCmd.Common;
using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Majorsilence.CrystalCmd.Server.Controllers
{
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + ",Basic")]
    public class AnalyzerController : ControllerBase
    {
        private readonly ILogger<AnalyzerController> _logger;
        private readonly IConfiguration _configuration;

        public AnalyzerController(ILogger<AnalyzerController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [HttpPost("/analyzer")]
        public async Task<IActionResult> Analyze()
        {
            var headers = Request.Headers;

            var baseRoute = new BaseRoute(_logger);
            var inputResults = await baseRoute.ReadInput(Request.Body, Request.ContentType, BaseRoute.HeadersFromAsp(headers), templateOnly: true);

            byte[] jsonBytes;
            try
            {
                jsonBytes = await AnalyzerResultsBytes(inputResults.ReportTemplate);
            }
            catch
            {
                return StatusCode(500);
            }

            return File(jsonBytes, "application/json");
        }

        private async Task<byte[]> AnalyzerResultsBytes(byte[] report)
        {
            string id = Guid.NewGuid().ToString();
            var queue = WorkQueue.CreateDefault("crystal-analyzer", _configuration);
            await queue.Enqueue(new QueueItem()
            {
                Data = null,
                ReportTemplate = report,
                Id = id
            });

            FullReportAnalysisResponse response = null;
            for (int i = 0; i < 60; i++)
            {
                var result = await queue.Get(id);
                if (result.Status == WorkItemStatus.Processing || result.Status == WorkItemStatus.Pending)
                {
                    await Task.Delay(500); // Wait before polling again
                    continue;
                }
                else if (result.Status == WorkItemStatus.Completed)
                {
                    response = JsonConvert.DeserializeObject<FullReportAnalysisResponse>(Encoding.UTF8.GetString(result.Report.FileContent));
                    break;
                }
                else
                {
                    throw new Exception("Analyzer failed");
                }
            }

            var jsonResponse = JsonConvert.SerializeObject(response);
            return Encoding.UTF8.GetBytes(jsonResponse);
        }
    }
}
