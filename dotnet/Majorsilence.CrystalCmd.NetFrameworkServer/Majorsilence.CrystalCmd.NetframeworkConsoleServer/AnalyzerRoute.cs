using EmbedIO;
using Majorsilence.CrystalCmd.Server.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    internal class AnalyzerRoute : BaseRoute
    {
        public AnalyzerRoute(ILogger logger) : base(logger)
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

            var inputResults = await ReadInput(inputStream, contentType, headers, templateOnly: true);
            await AnalyzerResults(ctx, inputResults.ReportTemplate);

        }

        private static async Task AnalyzerResults(IHttpContext ctx, byte[] report)
        {
            var analyzer = new CrystalReportsAnalyzer();
            var response = analyzer.GetFullAnalysis(report);
            // Convert the response object to JSON
            string jsonResponse = JsonConvert.SerializeObject(response);

            // Convert the JSON string to bytes
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonResponse);

            // Set the headers for content disposition and content type
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = jsonBytes.Length;
            ctx.Response.StatusCode = 200;

            // Write the byte array to the response stream
            await ctx.Response.OutputStream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
            await ctx.Response.OutputStream.FlushAsync();

            // Ensure that all headers and content are sent
            ctx.Response.Close();
        }
    }
}
