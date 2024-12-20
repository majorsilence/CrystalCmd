﻿using Majorsilence.CrystalCmd.NetFrameworkServer.Controllers;
using Majorsilence.CrystalCmd.Server.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using NSwag.AspNet.Owin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Majorsilence.CrystalCmd.NetFrameworkServer
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        private static HealthCheckTask _backgroundHealthTask;
        public static IServiceProvider ServiceProvider { get; private set; }

        protected void Application_Start()
        {
            string workingfolder = WorkingFolder.GetMajorsilenceTempFolder();
            if (System.IO.Directory.Exists(workingfolder))
            {
                System.IO.Directory.Delete(workingfolder, true);
            }
            System.IO.Directory.CreateDirectory(workingfolder);

            RouteTable.Routes.MapOwinPath("swagger", app =>
            {
                app.UseSwaggerUi(typeof(WebApiApplication).Assembly, settings =>
                {
                    settings.MiddlewareBasePath = "/swagger";
                    //settings.GeneratorSettings.DefaultUrlTemplate = "api/{controller}/{id}";  //this is the default one
                    settings.GeneratorSettings.DefaultUrlTemplate = "{controller}/{action}/{id}";
                });
            });

            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();
            var logger = ServiceProvider.GetService<ILogger>();

            string rptFilePath = Server.MapPath("~/bin/thereport.rpt");
            _backgroundHealthTask = new HealthCheckTask(logger, rptFilePath, false);
            _backgroundHealthTask.Start();
        }

        protected void Application_End(object sender, EventArgs e)
        {
            _backgroundHealthTask?.Stop();
        }

        private void ConfigureServices(IServiceCollection services)
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
                if (!Path.IsPathRooted(logFile))
                {
                    string appDataFolder = HostingEnvironment.MapPath("~/App_Data");
                    logFile = Path.Combine(appDataFolder, logFile);
                }

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
            string assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", args.Name.Split(',')[0] + ".dll");
            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }
            return null;
        }
    }


}
