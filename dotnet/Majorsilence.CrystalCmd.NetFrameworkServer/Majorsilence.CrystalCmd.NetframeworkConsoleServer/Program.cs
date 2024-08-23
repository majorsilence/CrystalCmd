using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Configuration;
using System.Runtime.Remoting.Contexts;
using System.Security.AccessControl;
using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.Files;
using Majorsilence.CrystalCmd.Server.Common;
using Swan.Logging;
using System.Runtime.Remoting.Messaging;
using System.Net.Mime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Web;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    internal class Program
    {
      
        private static HealthCheckTask _backgroundHealthTask;
        private static ServiceProvider _serviceProvider;

        static async Task Main(string[] args)
        {
            string workingfolder = WorkingFolder.GetMajorsilenceTempFolder();
            if (System.IO.Directory.Exists(workingfolder))
            {
                System.IO.Directory.Delete(workingfolder, true);
            }
            System.IO.Directory.CreateDirectory(workingfolder);

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = _serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>();

            string runndingDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string rptPath = System.IO.Path.Combine(runndingDir, "thereport.rpt");
            _backgroundHealthTask = new HealthCheckTask(logger, rptPath, true);
            _backgroundHealthTask.Start();

            int port = 44355;
            int.TryParse(Settings.GetSetting("Port"), out port);
            var url = $"http://*:{port}/";

            // Our web server is disposable.
            using (var server = CreateWebServer(url))
            {
                // Once we've registered our modules and configured them, we call the RunAsync() method.
                await server.RunAsync();

                while (true)
                {
                    await Task.Delay(1000);
                }
            }
        }

        // Create and configure our web server.
        private static WebServer CreateWebServer(string url)
        {
            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                .WithModule(new ActionModule("/status", HttpVerbs.Any, async (ctx) =>
                    {
                        await SendResponse_Wrapper("/status", ctx.Request.Headers, ctx.Request.InputStream,
                           ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx);
                    }))
                .WithModule(new ActionModule("/healthz", HttpVerbs.Any, async (ctx) =>
                {
                    await SendResponse_Wrapper(ctx.Request.RawUrl, ctx.Request.Headers, ctx.Request.InputStream,
                       ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx);
                }))
                .WithModule(new ActionModule("/export", HttpVerbs.Any, async (ctx) =>
                {
                    await SendResponse_Wrapper("/export", ctx.Request.Headers, ctx.Request.InputStream,
                        ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx);
                }))
                .WithModule(new ActionModule("/analyzer", HttpVerbs.Any, async (ctx) =>
                {
                    await SendResponse_Wrapper("/analyzer", ctx.Request.Headers, ctx.Request.InputStream,
                                     ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx);
                }));

            // Listen for state changes.
            server.StateChanged += (s, e) => $"WebServer New State - {e.NewState}".Info();

            return server;
        }


        static async Task
    SendResponse_Wrapper(string rawurl, System.Collections.Specialized.NameValueCollection headers,
      Stream inputStream,
      System.Text.Encoding inputContentEncoding,
      string contentType,
      IHttpContext ctx
    )
        {
            try
            {
                await SendResponse(rawurl, headers, inputStream, inputContentEncoding, contentType, ctx);
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>();
                logger.LogError(ex, "Error processing request");
                ctx.Response.StatusCode = 500;
            }
        }

        static async Task
           SendResponse(string rawurl, System.Collections.Specialized.NameValueCollection headers,
             Stream inputStream,
             System.Text.Encoding inputContentEncoding,
             string contentType,
             IHttpContext ctx
           )
        {
            if (string.Equals(rawurl, "/status", StringComparison.InvariantCultureIgnoreCase)
                || string.Equals(rawurl, "/healthz", StringComparison.InvariantCultureIgnoreCase)
                || string.Equals(rawurl, "/healthz/live", StringComparison.InvariantCultureIgnoreCase))
            {
                //  return (200, "I'm alive", "text/plain; charset=UTF-8", inputContentEncoding);
                await ctx.SendStringAsync("I'm alive", "text/plain", Encoding.UTF8);
                return;
            }
            else if (string.Equals(rawurl, "/healthz/ready", StringComparison.InvariantCultureIgnoreCase))
            {
                if (Server.Common.HealthCheckTask.IsHealthy)
                {
                    // Return a 200 OK status code
                    await ctx.SendStringAsync("Ready", "text/plain", Encoding.UTF8);
                    return;
                }
                //  return (200, "I'm alive", "text/plain; charset=UTF-8", inputContentEncoding);
                ctx.Response.StatusCode = 500;
                await ctx.SendStringAsync("Internal Server Error", "text/plain", Encoding.UTF8);
                return;
            }
            else if (string.Equals(rawurl, "/export", StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals(rawurl, "/analyzer", StringComparison.InvariantCultureIgnoreCase))
            {
                var creds = CustomServerSecurity.GetUserNameAndPassword(headers);
                var token = CustomServerSecurity.GetBearerToken(headers);
                string user = Settings.GetSetting("Username");
                string password = Settings.GetSetting("Password");
                string jwtKey = Settings.GetSetting("JwtKey");
                var expected_creds = (user, password);
                var callResult = Authenticate(creds, expected_creds, jwtKey, token);
                if (callResult.StatusCode != 200)
                {
                    ctx.Response.StatusCode = callResult.StatusCode;
                    return;
                }

                var streamContent = new StreamContent(inputStream);
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

                var provider = await streamContent.ReadAsMultipartAsync();
                CrystalCmd.Common.Data reportData = null;
                byte[] reportTemplate = null;

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




                string reportPath = null;
                try
                {
                    reportPath = Path.Combine(WorkingFolder.GetMajorsilenceTempFolder(), $"{Guid.NewGuid().ToString()}.rpt");
                    // System.IO.File.WriteAllBytes(reportPath, reportTemplate);
                    // Using System.IO.File.WriteAllBytes randomly causes problems where the system still 
                    // has the file open when crystal attempts to load it and crystal fails.
                    using (var fstream = new FileStream(reportPath, FileMode.Create))
                    {
                        await fstream.WriteAsync(reportTemplate, 0, reportTemplate.Length);
                        await fstream.FlushAsync();
                        fstream.Close();
                    }
                }
                catch (Exception ex)
                {
                    var logger = _serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>();
                    logger.LogError(ex, "Error saving report file");

                    ctx.Response.StatusCode = 500;
                    try
                    {
                        System.IO.File.Delete(reportPath);
                    }
                    catch (Exception)
                    {
                        // TODO: cleanup will happen later
                    }
                    return;
                }

                if (string.Equals(rawurl, "/analyzer", StringComparison.InvariantCultureIgnoreCase))
                {
                    await AnalyzerResults(ctx, reportPath);
                    return;
                }
                else
                {
                    await ExportReport(ctx, reportData, reportPath);
                    return;
                }

            }
            ctx.Response.StatusCode = 400;
        }

        private static async Task AnalyzerResults(IHttpContext ctx, string reportPath)
        {
            var analyzer = new CrystalReportsAnalyzer();
            var response = analyzer.GetFullAnalysis(reportPath);
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

        private static async Task ExportReport(IHttpContext ctx, CrystalCmd.Common.Data reportData, string reportPath)
        {
            byte[] bytes = null;
            string fileExt = "pdf";
            string mimeType = "application/octet-stream";
            try
            {
                var logger = _serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>();
                var exporter = new Majorsilence.CrystalCmd.Server.Common.Exporter(logger);
                var output = exporter.exportReportToStream(reportPath, reportData);
                bytes = output.Item1;
                fileExt = output.Item2;
                mimeType = output.Item3;

            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>();
                logger.LogError(ex, "Error exporting report");

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

        static (int StatusCode, string message) Authenticate((string UserName, string Password)? credentials,
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

        static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(configure =>
            {
                configure.ClearProviders();
                configure.AddConsole();
            })
            .Configure<LoggerFilterOptions>(options => options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Information);

            services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(s =>
            {
                return s.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("CrystalCmd");
            });
        }
    }
}
