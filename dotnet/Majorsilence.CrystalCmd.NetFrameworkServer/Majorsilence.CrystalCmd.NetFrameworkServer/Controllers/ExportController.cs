﻿using Majorsilence.CrystalCmd.Common;
using Majorsilence.CrystalCmd.Server.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;


namespace Majorsilence.CrystalCmd.NetFrameworkServer.Controllers
{
    public class ExportController : ApiController
    {
        private readonly ILogger _logger;
        public ExportController()
        {
            _logger = WebApiApplication.ServiceProvider.GetService<ILogger>();
        }

        [HttpPost]
        public async Task<HttpResponseMessage> Post()
        {
            if (ServerSetup.AuthFailed())
            {
                var authproblem = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                return authproblem;
            }

            Data reportData;
            byte[] reportTemplate;
            var input = await ReadInput();
            reportData = input.ReportData;
            reportTemplate = input.Template;

            string reportPath = null;
            byte[] bytes = null;
            string fileExt = "pdf";
            string mimeType = "application/octet-stream";
            try
            {
                string workingDir = WorkingFolder.GetMajorsilenceTempFolder();
                reportPath = Path.Combine(workingDir, $"{Guid.NewGuid().ToString()}.rpt");
                if (!Directory.Exists(workingDir))
                {
                    Directory.CreateDirectory(workingDir);
                }
                // System.IO.File.WriteAllBytes(reportPath, reportTemplate);
                // Using System.IO.File.WriteAllBytes randomly causes problems where the system still 
                // has the file open when crystal attempts to load it and crystal fails.
                using (var fstream = new FileStream(reportPath, FileMode.Create))
                {
                    fstream.Write(reportTemplate, 0, reportTemplate.Length);
                    fstream.Flush();
                    fstream.Close();
                }

                var exporter = new Majorsilence.CrystalCmd.Server.Common.Exporter(_logger);
                var output = exporter.exportReportToStream(reportPath, reportData);
                bytes = output.Item1;
                fileExt = output.Item2;
                mimeType = output.Item3;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report ({TraceId})", reportData?.TraceId ?? "");

                var message = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(ex.Message + System.Environment.NewLine + ex.StackTrace)
                };

                return message;
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

            var result = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            result.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
            {
                FileName = $"report.{fileExt}"
            };
            result.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            return result;
        }

        private async Task<(Data ReportData, byte[] Template)> ReadInput()
        {
            Data reportData = null;
            byte[] reportTemplate = null;

            if (Request.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                var result = await CheckForCompressedStreamInput();
                reportData = result.ReportData;
                reportTemplate = result.Template;
            }
            else
            {
                if (!Request.Content.IsMimeMultipartContent())
                    throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);

                var provider = new MultipartMemoryStreamProvider();
                await Request.Content.ReadAsMultipartAsync(provider);

                foreach (var file in provider.Contents)
                {
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

            return (reportData, reportTemplate);
        }

        private async Task<(Data ReportData, byte[] Template)> CheckForCompressedStreamInput()
        {
            // Decompress the content
            using (var originalStream = await Request.Content.ReadAsStreamAsync())
            using (var decompressedStream = new GZipStream(originalStream, CompressionMode.Decompress))
            using (var memoryStream = new MemoryStream())
            {
                await decompressedStream.CopyToAsync(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                // Replace the original content with the decompressed content
                Request.Content = new StreamContent(memoryStream);
                foreach (var header in Request.Content.Headers)
                {
                    Request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                var input = await Request.Content.ReadAsStringAsync();
                var dto = JsonConvert.DeserializeObject<StreamedRequest>(input);
                return (dto.ReportData, dto.Template);
            }
        }
    }
}
