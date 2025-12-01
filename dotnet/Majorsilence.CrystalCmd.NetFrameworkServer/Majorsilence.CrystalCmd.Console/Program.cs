using Majorsilence.CrystalCmd.Server.Common;
using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.NetframeworkConsole
{
    internal class Program
    {
        private static ServiceProvider _serviceProvider;
        private static HealthCheckTask _backgroundHealthTask;

        public static async Task Main(string[] args)
        {
            foreach (var t in args)
            {
                if (t == "/?" || t == "-help" || t == "help" || t == "--help")
                {
                    PrintHelp();
                    Environment.Exit(0);
                }
            }

            var workingDir = WorkingFolder.GetMajorsilenceTempFolder();
            if (!System.IO.Directory.Exists(workingDir))
            {
                System.IO.Directory.CreateDirectory(workingDir);
            }

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();

            var logger = _serviceProvider.GetService<ILogger>();

            RunBackHealthChecks(logger);

            bool isSqlite = string.Equals(WorkQueue.GetSetting("WorkQueueSqlType"), "sqlite", StringComparison.InvariantCultureIgnoreCase);
            int threadCount = isSqlite ? 1 : Environment.ProcessorCount;

            if (Environment.UserInteractive)
            {
                var exporters =  ExportQueue.Create(logger, "crystal-reports", threadCount);
                foreach(var exporter in exporters)
                {
                    exporter.Start();
                }

                // crystal-analyzer
                var analyzerExport = new ExportQueue(logger, "crystal-analyzer");
                analyzerExport.Start();

                Console.WriteLine("Press ctrl+c to stop the CrystalCmd report processing service...");

                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            else
            {
                const string serviceName = "CrystalCmdService";
                ServiceBase.Run(new WinService(serviceName, logger));
            }

        }

        private static void RunBackHealthChecks(ILogger logger)
        {
            string runndingDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string rptPath = System.IO.Path.Combine(runndingDir, "thereport.rpt");
            _backgroundHealthTask = new HealthCheckTask(logger, rptPath, true);
            _backgroundHealthTask.Start();
        }


        static void PrintHelp()
        {
            Console.WriteLine("Required options");
            Console.WriteLine("Set the appsettings_WorkQueueSqlType and the appsettings_WorkQueueSqlConnection environment variable");
            Console.WriteLine(
                ".\\Majorsilence.CrystalCmd.NetframeworkConsole.exe");
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