using CrystalDecisions.CrystalReports.Engine;
using EmbedIO;
using Majorsilence.CrystalCmd.Server.Common;
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
using System.Windows.Controls;

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
                BackgroundQueue.Instance.QueueThread(async () =>
                {
                    try
                    {
                        var exporter = new Majorsilence.CrystalCmd.Server.Common.Exporter(_logger);
                        var output = exporter.exportReportToStream(inputResults.ReportPath, inputResults.ReportData);

                        if (output == null)
                        {
                            File.WriteAllText(System.IO.Path.Combine(inputResults.WorkingFolder.FullName, $"{inputResults.Id}.error"), "output was null");
                            _logger.LogError("Error exporting report ({TraceId})", inputResults.ReportData?.TraceId ?? "");

                            return;
                        }

                        var bytes = output.Item1;
                        var fileExt = output.Item2;
                        var mimeType = output.Item3;
                        string outputFilename = System.IO.Path.Combine(inputResults.WorkingFolder.FullName, $"{inputResults.Id}.{fileExt}");
                        System.IO.File.WriteAllBytes(outputFilename, bytes);
                        File.WriteAllText(System.IO.Path.Combine(inputResults.WorkingFolder.FullName, $"{inputResults.Id}.exported"), $"{outputFilename}\n{mimeType}");
                    }
                    catch(Exception ex)
                    {
                        File.WriteAllText(System.IO.Path.Combine(inputResults.WorkingFolder.FullName, $"{inputResults.Id}.error"), "output was null");
                        _logger.LogError(ex, "Error exporting report ({TraceId})", inputResults.ReportData?.TraceId ?? "");
                    }
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

                var folder = new DirectoryInfo(Path.Combine(WorkingFolder.GetMajorsilenceTempFolder(), id));
                if (!folder.Exists)
                {
                    ctx.Response.StatusCode = 404;
                    return;
                }
                if (folder.GetFiles().Any(f => f.Name.EndsWith(".exported")))
                {
                    ctx.Response.StatusCode = 200;
                    var file = folder.GetFiles().First(f => f.Name.EndsWith(".exported"));
                    var lines = File.ReadAllLines(file.FullName);
                    var filename = lines[0];
                    var mimeType = lines[1];
                    ctx.Response.Headers.Add("Content-Disposition", $"attachment; filename={file.Name}");
                    ctx.Response.ContentType = mimeType;
                    ctx.Response.ContentLength64 = new FileInfo(filename).Length;
                    ctx.Response.StatusCode = 200;
                    await ctx.Response.OutputStream.WriteAsync(File.ReadAllBytes(filename), 0, (int)ctx.Response.ContentLength64);
                    await ctx.Response.OutputStream.FlushAsync();
                    ctx.Response.Close();

                    folder.Delete(true);
                }
                else if (folder.GetFiles().Any(f => f.Name.EndsWith(".error")))
                {
                    folder.Delete(true);
                    ctx.Response.StatusCode = 500;
                }
                else
                {
                    ctx.Response.StatusCode = 202;
                    await ctx.SendStringAsync("Processing report", "text/plain", Encoding.UTF8);
                }
            }
        }
    }
}
