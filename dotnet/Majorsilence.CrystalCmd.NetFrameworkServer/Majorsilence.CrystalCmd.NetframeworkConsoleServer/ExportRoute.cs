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
    internal class ExportRoute : BaseRoute
    {
        public ExportRoute(ILogger logger) : base(logger)
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

            var inputResults = await ReadInput(inputStream, contentType, headers);
            var queue = WorkQueue.CreateDefault();
            await queue.Enqueue(new QueueItem()
            {
                Data = inputResults.ReportData,
                ReportTemplate = inputResults.ReportTemplate,
                Id = inputResults.Id
            });


            await ExportReport(ctx, inputResults.Id, queue, inputResults.ReportData?.TraceId);
        }

        private async Task ExportReport(IHttpContext ctx, string id, WorkQueue workQueue, string traceId)
        {
            byte[] bytes = null;
            string fileExt = "pdf";
            string mimeType = "application/octet-stream";
            try
            {

                for (int i = 0; i < 60; i++)
                {
                    var result = await workQueue.Get(id);
                    if (result.Status == WorkItemStatus.Processing || result.Status == WorkItemStatus.Pending)
                    {
                        await Task.Delay(500); // Wait before polling again
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
                        ctx.Response.StatusCode = 500;
                        return;
                    }
                }

                if (bytes == null)
                {
                    ctx.Response.StatusCode = 500;
                    return;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report ({TraceId})", traceId ?? "");

                ctx.Response.StatusCode = 500;
                return;
            }

            // Set the headers for content disposition and content type
            ctx.Response.Headers.Add("Content-Disposition", $"attachment; filename=report.{fileExt}");
            ctx.Response.ContentType = mimeType;
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.StatusCode = 200;

            // Write the byte array to the response stream
            await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            await ctx.Response.OutputStream.FlushAsync();

            // Ensure that all headers and content are sent
            ctx.Response.Close();
        }
    }
}
