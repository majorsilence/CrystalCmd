using CrystalDecisions.CrystalReports.Engine;
using EmbedIO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Swan.Parsers;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
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
            await ExportReport(ctx, inputResults.ReportData, inputResults.ReportPath);
        }

        private async Task ExportReport(IHttpContext ctx, CrystalCmd.Common.Data reportData, string reportPath)
        {
            byte[] bytes = null;
            string fileExt = "pdf";
            string mimeType = "application/octet-stream";
            try
            {
                var exporter = new Majorsilence.CrystalCmd.Server.Common.Exporter(_logger);
                var output = exporter.exportReportToStream(reportPath, reportData);
                bytes = output.Item1;
                fileExt = output.Item2;
                mimeType = output.Item3;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report ({TraceId})", reportData?.TraceId ?? "");

                ctx.Response.StatusCode = 500;
                return;
            }
            finally
            {
                try
                {
                    System.IO.File.Delete(reportPath);
                }
                catch (Exception)
                {
                    // TODO: cleanup will happen later
                }
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
