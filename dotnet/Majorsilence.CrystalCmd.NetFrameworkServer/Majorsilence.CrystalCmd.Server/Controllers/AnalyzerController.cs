using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
            // Authenticate
            var callResult = new BaseRoute(_logger).Authenticate(CustomServerSecurity.GetNameValueCollection(headers));
            if (callResult.StatusCode != 200)
                return StatusCode(callResult.StatusCode);

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
