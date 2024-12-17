using EmbedIO;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    internal class StatusRoute : BaseRoute
    {
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
                await ctx.SendStringAsync("I'm alive", "text/plain", Encoding.UTF8);
            }
            else if (string.Equals(rawurl, "/healthz/ready", StringComparison.InvariantCultureIgnoreCase))
            {
                if (Server.Common.HealthCheckTask.IsHealthy)
                {
                    ctx.Response.StatusCode = 200;
                    await ctx.SendStringAsync("Ready", "text/plain", Encoding.UTF8);
                    return;
                }
                ctx.Response.StatusCode = 500;
                await ctx.SendStringAsync("Internal Server Error", "text/plain", Encoding.UTF8);
            }
        }
    }
}
