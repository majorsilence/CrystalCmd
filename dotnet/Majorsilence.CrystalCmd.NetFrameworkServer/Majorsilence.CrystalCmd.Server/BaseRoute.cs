using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading.Tasks;
using Majorsilence.CrystalCmd.Common;
using System.Collections.Specialized;

namespace Majorsilence.CrystalCmd.Server
{
    internal class BaseRoute
    {
        protected readonly ILogger _logger;
        public BaseRoute(ILogger logger)
        {
            _logger = logger;
        }

        public static NameValueCollection HeadersFromAsp(Microsoft.AspNetCore.Http.IHeaderDictionary headers)
        {
            var nvc = new NameValueCollection();
            if (headers == null) return nvc;
            foreach (var kv in headers)
            {
                // StringValues.ToString() returns a comma-separated string without allocating an array
                nvc.Add(kv.Key, kv.Value.ToString());
            }
            return nvc;
        }

        public async Task<(Data ReportData, byte[] ReportTemplate, string Id)> ReadInput(Stream inputStream, string contentType,
          NameValueCollection headers,
          bool templateOnly = false)
        {
            if (inputStream == null) throw new CrystalCmdException("input stream is null");

            Data reportData = null;
            byte[] reportTemplate = null;

            if (string.IsNullOrWhiteSpace(contentType))
            {
                throw new CrystalCmdException("content type is null");
            }

            // avoid allocation from ToLower()
            bool contentIndicatesGzip = contentType.IndexOf("gzip", StringComparison.OrdinalIgnoreCase) >= 0
                                        || string.Equals(headers?["Content-Encoding"] ?? "", "gzip", StringComparison.OrdinalIgnoreCase);

            if (contentIndicatesGzip)
            {
                var result = await CompressedStreamInput(inputStream).ConfigureAwait(false);
                reportData = result.ReportData;
                reportTemplate = result.Template;
            }
            else
            {
                using (var streamContent = new StreamContent(inputStream))
                {
                    streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

                    if (!streamContent.IsMimeMultipartContent())
                        throw new InvalidOperationException("Unsupported media type");

                    var provider = await streamContent.ReadAsMultipartAsync().ConfigureAwait(false);
                    foreach (var file in provider.Contents)
                    {
                        string name = file.Headers.ContentDisposition?.Name?.Replace("\"", "") ?? "";
                        if (string.Equals(name, "reportdata", StringComparison.CurrentCultureIgnoreCase))
                        {
                            var json = await file.ReadAsStringAsync().ConfigureAwait(false);
                            reportData = JsonConvert.DeserializeObject<CrystalCmd.Common.Data>(json);
                        }
                        else
                        {
                            reportTemplate = await file.ReadAsByteArrayAsync().ConfigureAwait(false);
                        }
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

        private static async Task<(Data ReportData, byte[] Template)> CompressedStreamInput(Stream inputStream)
        {
            if (inputStream == null)
            {
                throw new CrystalCmdException("CompressedStreamInput inputStream is null");
            }

            // Decompress and deserialize directly from stream to avoid intermediate MemoryStream and extra allocations.
            using (var decompressed = new GZipStream(inputStream, CompressionMode.Decompress))
            using (var sr = new StreamReader(decompressed))
            using (var jsonReader = new JsonTextReader(sr))
            {
                var serializer = new JsonSerializer();
                var dto = serializer.Deserialize<StreamedRequest>(jsonReader);
                if (dto == null)
                {
                    throw new CrystalCmdException("CompressedStreamInput dto is null");
                }
                return (dto.ReportData, dto.Template);
            }
        }
    }
}
