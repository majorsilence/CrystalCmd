using EmbedIO;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    internal class StatusRoute : BaseRoute
    {
        private static string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public StatusRoute(ILogger logger) : base(logger)
        {
        }

        protected override async Task SendResponse_Internal(string rawurl, NameValueCollection headers, Stream inputStream, Encoding inputContentEncoding, string contentType, IHttpContext ctx)
        {
            if (string.Equals(rawurl, "/status", StringComparison.InvariantCultureIgnoreCase)
               || string.Equals(rawurl, "/healthz", StringComparison.InvariantCultureIgnoreCase)
               || string.Equals(rawurl, "/healthz/live", StringComparison.InvariantCultureIgnoreCase))
            {
                ctx.Response.StatusCode = 200;
                await ctx.SendStringAsync($"I'm alive {version}", "text/plain", Encoding.UTF8);
            }
            else if (string.Equals(rawurl, "/healthz/ready", StringComparison.InvariantCultureIgnoreCase))
            {
                ctx.Response.StatusCode = 200;
                await ctx.SendStringAsync($"Ready {version}", "text/plain", Encoding.UTF8);
            }
        }
    }
}
