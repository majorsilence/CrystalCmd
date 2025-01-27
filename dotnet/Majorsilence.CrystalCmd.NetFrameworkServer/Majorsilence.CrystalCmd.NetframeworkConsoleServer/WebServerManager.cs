using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Actions;
using System.Threading;
using Majorsilence.CrystalCmd.Server.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using Swan.Logging;
using System.Reflection;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.IO;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    public class WebServerManager
    {
        private readonly WebServer _server;
        private static HealthCheckTask _backgroundHealthTask;
        private static ServiceProvider _serviceProvider;

        public WebServerManager()
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
            int portHttps = 44356;
            int.TryParse(Settings.GetSetting("Port"), out port);
            int.TryParse(Settings.GetSetting("PortHttps"), out portHttps);
            var url = $"http://*:{port}/";
            var urlHttps = $"https://*:{portHttps}/";

            // Our web server is disposable.
            _server = CreateWebServer(url, urlHttps);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _server.RunAsync(cancellationToken);
        }

        public void Stop()
        {
            _server.Dispose();
        }

        private static WebServer CreateWebServer(string url, string urlHttps)
        {
            WebServerOptions serverOptions;

            try
            {
                var certificate = GenerateSelfSignedCertificate("CN=localhost");
                serverOptions = new WebServerOptions()
                   .WithUrlPrefix(url)
                   .WithUrlPrefix(urlHttps)
                   .WithMode(HttpListenerMode.EmbedIO);

                serverOptions = serverOptions.WithCertificate(certificate);
            }
            catch (Exception)
            {
                serverOptions = new WebServerOptions()
                   .WithUrlPrefix(url)
                   .WithMode(HttpListenerMode.EmbedIO);
            }


            var server = new WebServer(serverOptions)
            .WithModule(new ActionModule("/status", HttpVerbs.Any, async (ctx) =>
                {
                    var instance = new StatusRoute(_serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>());
                    await instance.SendResponse(ctx.Request.RawUrl, ctx.Request.Headers, ctx.Request.InputStream,
                       ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx);
                }))
            .WithModule(new ActionModule("/healthz", HttpVerbs.Any, async (ctx) =>
            {
                var instance = new StatusRoute(_serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>());
                await instance.SendResponse(ctx.Request.RawUrl, ctx.Request.Headers, ctx.Request.InputStream,
                   ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx);
            }))
            .WithModule(new ActionModule("/export/poll", HttpVerbs.Any, async (ctx) =>
            {
                var instance = new ExportPollRoute(_serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>());
                await instance.SendResponse(ctx.Request.RawUrl, ctx.Request.Headers, ctx.Request.InputStream,
                    ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx);
            }))
            .WithModule(new ActionModule("/export", HttpVerbs.Any, async (ctx) =>
            {
                var instance = new ExportRoute(_serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>());
                await instance.SendResponse(ctx.Request.RawUrl, ctx.Request.Headers, ctx.Request.InputStream,
                    ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx);
            }))
            .WithModule(new ActionModule("/analyzer", HttpVerbs.Any, async (ctx) =>
            {
                var instance = new AnalyzerRoute(_serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>());
                await instance.SendResponse(ctx.Request.RawUrl, ctx.Request.Headers, ctx.Request.InputStream,
                                 ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx);
            })).WithModule(new ActionModule("/", HttpVerbs.Any, (ctx) =>
            {
                ctx.Response.StatusCode = 400;
                return Task.CompletedTask;
            }));

            // Listen for state changes.
            server.StateChanged += (s, e) => $"WebServer New State - {e.NewState}".Info();

            return server;
        }

        static void ConfigureServices(IServiceCollection services)
        {
            string externalLogAssemblyPath = Settings.GetSetting("ExternalLogsAssemblyFilePath");
            string logFile = Settings.GetSetting("LogFile");
            if (!string.IsNullOrWhiteSpace(externalLogAssemblyPath))
            {
                string className = Settings.GetSetting("ExternalLogsAssemblyClassName");
                string functionName = Settings.GetSetting("ExternalLogsAssemblyFunctionName");

                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

                var assembly = Assembly.LoadFrom(externalLogAssemblyPath);
                var externalLogsHelperType = assembly.GetType(className);
                var addLoggerMethod = externalLogsHelperType.GetMethod(functionName, BindingFlags.Static | BindingFlags.Public);

                services.AddLogging(configure =>
                {
                    configure.ClearProviders();

                    addLoggerMethod.Invoke(null, new object[] { configure });

                })
               .Configure<LoggerFilterOptions>(options =>
               {
                   options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Information;
               });
            }
            else if (!string.IsNullOrWhiteSpace(logFile))
            {
                services.AddLogging(configure =>
                {
                    configure.ClearProviders();
                    configure.AddFile(logFile, (options) =>
                    {
                        options.Append = true;
                        options.FileSizeLimitBytes = 5000000;
                        options.MaxRollingFiles = 10;
                    });
                })
               .Configure<LoggerFilterOptions>(options =>
               {
                   options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Information;
               });
            }
            else
            {
                services.AddLogging(configure =>
                {
                    configure.ClearProviders();
                    configure.AddConsole();
                })
                .Configure<LoggerFilterOptions>(options => options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Information);
            }

            services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(s =>
            {
                return s.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("CrystalCmd");
            });
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, args.Name.Split(',')[0] + ".dll");
            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }
            return null;
        }

        static X509Certificate2 GenerateSelfSignedCertificate(string subjectName)
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                // Add extensions to the certificate (e.g., for server authentication)
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // OID for Server Authentication
                        false));
                // Create the self-signed certificate
                var certificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
                // Export the certificate to PFX format and re-import it to set the private key
                return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
            }
        }
    }
}
