using EmbedIO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Majorsilence.CrystalCmd.Common;
using Majorsilence.CrystalCmd.Server.Common;
using System.Runtime.Remoting.Messaging;
using System.Net;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    internal class BaseRoute
    {
        protected readonly ILogger _logger;
        public BaseRoute(ILogger logger)
        {
            _logger = logger;
        }

        public async Task SendResponse(string rawurl,
            System.Collections.Specialized.NameValueCollection headers,
            Stream inputStream,
            System.Text.Encoding inputContentEncoding,
            string contentType,
            IHttpContext ctx
           )
        {
            try
            {
                await SendResponse_Internal(rawurl, headers, inputStream, inputContentEncoding, contentType, ctx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request");
                ctx.Response.StatusCode = 500;
            }
        }

        protected virtual Task SendResponse_Internal(
             string rawurl,
             System.Collections.Specialized.NameValueCollection headers,
             Stream inputStream,
             System.Text.Encoding inputContentEncoding,
             string contentType,
             IHttpContext ctx
        )
        {
            return Task.CompletedTask;
        }

        protected (int StatusCode, string message) Authenticate(System.Collections.Specialized.NameValueCollection headers)
        {
            var creds = CustomServerSecurity.GetUserNameAndPassword(headers);
            var token = CustomServerSecurity.GetBearerToken(headers);
            string user = Settings.GetSetting("Username");
            string password = Settings.GetSetting("Password");
            string jwtKey = Settings.GetSetting("JwtKey");
            var expected_creds = (user, password);
            return Authenticate_Internal(creds, expected_creds, jwtKey, token);
        }

        private static (int StatusCode, string message) Authenticate_Internal((string UserName, string Password)? credentials,
         (string UserName, string Password) expected_credentials,
         string jwtKey, string token)
        {

            if (!string.IsNullOrWhiteSpace(jwtKey))
            {
                if (!string.IsNullOrWhiteSpace(token) && TokenVerifier.VerifyToken(token, jwtKey))
                {
                    return (200, "");
                }
            }

            if (!string.Equals(credentials?.UserName, expected_credentials.UserName, StringComparison.InvariantCultureIgnoreCase)
                || !string.Equals(credentials?.Password, expected_credentials.Password, StringComparison.InvariantCulture))
            {
                return (401, "Unauthorized");
            }
            return (200, "");
        }

        protected async Task<(Data ReportData, byte[] ReportTemplate, string Id)> ReadInput(Stream inputStream, string contentType,
          System.Collections.Specialized.NameValueCollection headers,
          bool templateOnly = false)
        {
            var streamContent = new StreamContent(inputStream);
            Data reportData = null;
            byte[] reportTemplate = null;

            if(string.IsNullOrWhiteSpace(contentType))
            {
                throw new CrystalCmdException("content type is null");
            }

            if (contentType.ToLower().Contains("gzip") || string.Equals(headers["Content-Encoding"] ?? "", "gzip", StringComparison.OrdinalIgnoreCase))
            {
                var result = await CompressedStreamInput(streamContent);
                reportData = result.ReportData;
                reportTemplate = result.Template;
            }
            else
            {
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

                if (!streamContent.IsMimeMultipartContent())
                    throw new EmbedIO.HttpException(HttpStatusCode.UnsupportedMediaType);

                var provider = await streamContent.ReadAsMultipartAsync();
                foreach (var file in provider.Contents)
                {
                    // https://stackoverflow.com/questions/7460088/reading-file-input-from-a-multipart-form-data-post
                    string name = file.Headers.ContentDisposition.Name.Replace("\"", "");
                    if (string.Equals(name, "reportdata", StringComparison.CurrentCultureIgnoreCase))
                    {
                        reportData = Newtonsoft.Json.JsonConvert.DeserializeObject<CrystalCmd.Common.Data>(await file.ReadAsStringAsync());
                    }
                    else
                    {
                        reportTemplate = await file.ReadAsByteArrayAsync();
                    }
                }
            }

            if (!templateOnly && reportData == null)
            {
                throw new CrystalCmdException("report data is null");
            }

            if (reportTemplate == null)
            {
                throw new CrystalCmdException("report template is null");
            }

            string id = Guid.NewGuid().ToString();
            return (reportData, reportTemplate, id);
        }

        private static async Task<(Data ReportData, byte[] Template)> CompressedStreamInput(StreamContent content)
        {
            if (content == null)
            {
                throw new CrystalCmdException("CompressedStreamInput content is null");
            }

            // Decompress the content
            using (var originalStream = await content.ReadAsStreamAsync())
            using (var decompressedStream = new GZipStream(originalStream, CompressionMode.Decompress))
            using (var memoryStream = new MemoryStream())
            {
                await decompressedStream.CopyToAsync(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                // Replace the original content with the decompressed content
                content = new StreamContent(memoryStream);

                var input = await content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(input))
                {
                    throw new CrystalCmdException("CompressedStreamInput input is null");
                }

                var dto = JsonConvert.DeserializeObject<StreamedRequest>(input);
                if (dto == null)
                {
                    throw new CrystalCmdException("CompressedStreamInput dto is null");
                }
                return (dto.ReportData, dto.Template);
            }
        }
    }
}
