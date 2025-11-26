using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server.Controllers
{
    [ApiController]
    public class ExportController : ControllerBase
    {
        private readonly ILogger<ExportController> _logger;
        private readonly IConfiguration _configuration;

        public ExportController(ILogger<ExportController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [HttpPost("/export")]
        public async Task<IActionResult> Export()
        {
            var headers = Request.Headers;
            // Authenticate
            var callResult = new BaseRoute(_logger).Authenticate(CustomServerSecurity.GetNameValueCollection(headers));
            if (callResult.StatusCode != 200)
                return StatusCode(callResult.StatusCode);

            var baseRoute = new BaseRoute(_logger);
            var inputResults = await baseRoute.ReadInput(Request.Body, Request.ContentType, CustomServerSecurity.GetNameValueCollection(headers));
            var queue = WorkQueue.CreateDefault("crystal-reports", _configuration);
            await queue.Enqueue(new QueueItem()
            {
                Data = inputResults.ReportData,
                ReportTemplate = inputResults.ReportTemplate,
                Id = inputResults.Id
            });

            // Poll for result
            byte[] bytes = null;
            string fileExt = "pdf";
            string mimeType = "application/octet-stream";
            try
            {
                for (int i = 0; i < 60; i++)
                {
                    var result = await queue.Get(inputResults.Id);
                    if (result.Status == WorkItemStatus.Processing || result.Status == WorkItemStatus.Pending)
                    {
                        await Task.Delay(500);
                        continue;
                    }
                    else if (result.Status == WorkItemStatus.Completed)
                    {
                        bytes = result.Report.FileContent;
                        fileExt = result.Report.Format;
                        mimeType = result.Report.Format == "pdf" ? "application/pdf" : "application/octet-stream";
                        break;
                    }
                    else
                    {
                        return StatusCode(500, "Timed out waiting.  You should use the export/poll POST then GET flow. See https://github.com/majorsilence/CrystalCmd/wiki/Polled-report-generation-(export-poll).");
                    }
                }

                if (bytes == null)
                    return StatusCode(500);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report");
                return StatusCode(500);
            }

            return File(bytes, mimeType, $"report.{fileExt}");
        }

        [HttpPost("/export/poll")]
        public async Task<IActionResult> ExportPollPost()
        {
            var headers = Request.Headers;
            // Authenticate
            var callResult = new BaseRoute(_logger).Authenticate(CustomServerSecurity.GetNameValueCollection(headers));
            if (callResult.StatusCode != 200)
                return StatusCode(callResult.StatusCode);

            var baseRoute = new BaseRoute(_logger);
            var inputResults = await baseRoute.ReadInput(Request.Body, Request.ContentType, CustomServerSecurity.GetNameValueCollection(headers));
            var queue = WorkQueue.CreateDefault("crystal-reports", _configuration);
            await queue.Enqueue(new QueueItem()
            {
                Id = inputResults.Id,
                ReportTemplate = inputResults.ReportTemplate,
                Data = inputResults.ReportData
            });
            return Ok(inputResults.Id);
        }

        [HttpGet("/export/poll")]
        public async Task<IActionResult> ExportPollGet([FromHeader(Name = "id")] string id)
        {
            var headers = Request.Headers;
            // Authenticate
            var callResult = new BaseRoute(_logger).Authenticate(CustomServerSecurity.GetNameValueCollection(headers));
            if (callResult.StatusCode != 200)
                return StatusCode(callResult.StatusCode);

            if (string.IsNullOrWhiteSpace(id))
                return BadRequest();

            var queue = WorkQueue.CreateDefault("crystal-reports", _configuration);
            var result = await queue.Get(id);

            if (result.Status == WorkItemStatus.Unknown)
                return NotFound();
            if (result.Status == WorkItemStatus.Completed)
            {
                var bytes = result.Report.FileContent;
                var fileName = result.Report.FileName;
                var mimeType = result.Report.Format == "pdf" ? "application/pdf" : "application/octet-stream";
                return File(bytes, mimeType, fileName);
            }
            else if (result.Status == WorkItemStatus.Failed)
                return StatusCode(500);
            else if (result.Status == WorkItemStatus.Processing)
                return Accepted("Processing report");
            else
                return StatusCode(452, "Unknown");
        }
    }
}
