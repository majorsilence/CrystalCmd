using Majorsilence.CrystalCmd.WorkQueues;
using Majorsilence.CrystalCmd.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server.Controllers
{
    [ApiController]
    public class AnalyzerController : ControllerBase
    {
        private readonly ILogger<AnalyzerController> _logger;

        public AnalyzerController(ILogger<AnalyzerController> logger)
        {
            _logger = logger;
        }

        [HttpPost("/analyzer")]
        public async Task<IActionResult> Analyze()
        {
            var headers = Request.Headers;
            var baseRoute = new BaseRoute(_logger);
            var inputResults = await baseRoute.ReadInput(Request.Body, Request.ContentType, BaseRoute.HeadersFromAsp(headers), templateOnly: true);

            var analyzer = new AnalyzerRoute(_logger);
            byte[] jsonBytes;
            try
            {
                jsonBytes = await analyzer.AnalyzerResultsBytes(inputResults.ReportTemplate);
            }
            catch
            {
                return StatusCode(500);
            }

            return File(jsonBytes, "application/json");
        }
    }
}
