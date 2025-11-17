using EmbedIO;
using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    internal class ExportPollRoute : BaseRoute
    {
        public ExportPollRoute(ILogger logger) : base(logger)
        {
        }

        protected override async Task SendResponse_Internal(string rawurl, NameValueCollection headers, Stream inputStream, Encoding inputContentEncoding, string contentType, IHttpContext ctx)
        {
            var callResult = Authenticate(headers);
            if (callResult.StatusCode != 200)
            {
                ctx.Response.StatusCode = callResult.StatusCode;
                return;
            }

            if (string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                var inputResults = await ReadInput(inputStream, contentType, headers);
                var queue = WorkQueue.CreateDefault();
                await queue.Enqueue(new QueueItem()
                {
                    Id = inputResults.Id,
                    ReportTemplate = inputResults.ReportTemplate,
                    Data = inputResults.ReportData
                });
                await ctx.SendStringAsync(inputResults.Id, "text/plain", Encoding.UTF8);
            }
            else if (string.Equals(ctx.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                var id = headers["id"];

                if (string.IsNullOrWhiteSpace(id))
                {
                    ctx.Response.StatusCode = 400;
                    return;
                }

                var queue = WorkQueue.CreateDefault();
                var result = await queue.Get(id);

                if (result.Status == WorkItemStatus.Unknown)
                {
                    ctx.Response.StatusCode = 404;
                    return;
                }
                if (result.Status == WorkItemStatus.Completed)
                {
                    ctx.Response.StatusCode = 200;

                    ctx.Response.Headers.Add("Content-Disposition", $"attachment; filename={result.Report.FileName}");
                    ctx.Response.ContentType = result.Report.Format == "pdf" ? "application/pdf" : "application/octet-stream";
                    ctx.Response.ContentLength64 = result.Report.FileContent.Length;
                    ctx.Response.StatusCode = 200;
                    await ctx.Response.OutputStream.WriteAsync(result.Report.FileContent, 0, (int)ctx.Response.ContentLength64);
                    await ctx.Response.OutputStream.FlushAsync();
                    ctx.Response.Close();

                }
                else if (result.Status == WorkItemStatus.Failed)
                {
                    ctx.Response.StatusCode = 500;
                }
                else if (result.Status == WorkItemStatus.Processing)
                {
                    ctx.Response.StatusCode = 202;
                    await ctx.SendStringAsync("Processing report", "text/plain", Encoding.UTF8);
                }
                else
                {
                    ctx.Response.StatusCode = 452;
                    await ctx.SendStringAsync("Unknown", "text/plain", Encoding.UTF8);
                }
            }
        }
    }
}
