using EmbedIO.Routing;
using EmbedIO;
using EmbedIO.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    internal class ExportController : WebApiController
    {
        [Route(HttpVerbs.Post, "/export")]
        public async Task<string> Post()
        {
            if (!Request.Content.IsMimeMultipartContent())
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);

            if (AuthFailed())
            {
                var authproblem = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                return authproblem;
            }

            var provider = new MultipartMemoryStreamProvider();
            await Request.REad.Content.ReadAsMultipartAsync(provider);

            Client.Data reportData = null;
            byte[] reportTemplate = null;

            foreach (var file in provider.Contents)
            {
                string name = file.Headers.ContentDisposition.Name.Replace("\"", "");
                if (string.Equals(name, "reportdata", StringComparison.CurrentCultureIgnoreCase))
                {
                    reportData = Newtonsoft.Json.JsonConvert.DeserializeObject<Client.Data>(await file.ReadAsStringAsync());
                }
                else
                {
                    reportTemplate = await file.ReadAsByteArrayAsync();
                }
            }
        }
    }
}
