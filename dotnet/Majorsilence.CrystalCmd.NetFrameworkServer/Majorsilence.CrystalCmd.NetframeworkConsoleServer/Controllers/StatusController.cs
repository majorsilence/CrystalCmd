using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Majorsilence.CrystalCmd.Server.Controllers
{
    [ApiController]
    public class StatusController : ControllerBase
    {
        private readonly ILogger<StatusController> _logger;
        private static string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

        public StatusController(ILogger<StatusController> logger)
        {
            _logger = logger;
        }

        [HttpGet("/status")]
        [HttpGet("/healthz")]
        public IActionResult Status()
        {
            return Ok($"I'm alive {version}");
        }

        [HttpGet("/healthz/ready")]
        public IActionResult Ready()
        {
            return Ok($"Ready {version}");
        }
    }
}
