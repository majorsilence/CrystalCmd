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
using System.Web.Hosting;
using NReco.Logging.File;
using System.Reflection;
using Majorsilence.CrystalCmd.Common;
using System.IO.Compression;

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
                        var instance = new StatusRoute(_serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>());
                        await instance.SendResponse("/status", ctx.Request.Headers, ctx.Request.InputStream,
                           ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx);
                    }))
                .WithModule(new ActionModule("/healthz", HttpVerbs.Any, async (ctx) =>
                {
                    var instance = new StatusRoute(_serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>());
                    await instance.SendResponse(ctx.Request.RawUrl, ctx.Request.Headers, ctx.Request.InputStream,
                       ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx);
                }))
                .WithModule(new ActionModule("/export", HttpVerbs.Any, async (ctx) =>
                {
                    var instance = new ExportRoute(_serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>());
                    await instance.SendResponse("/export", ctx.Request.Headers, ctx.Request.InputStream,
                        ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx);
                }))
                .WithModule(new ActionModule("/analyzer", HttpVerbs.Any, async (ctx) =>
                {
                    var instance = new AnalyzerRoute(_serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>());
                    await instance.SendResponse("/analyzer", ctx.Request.Headers, ctx.Request.InputStream,
                                     ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx);
                })).WithModule(new ActionModule("/", HttpVerbs.Any, (ctx) => {
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
    }
}
