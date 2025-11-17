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
        private static string WorkingFolder;
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

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();

            var logger = _serviceProvider.GetService<ILogger>();

            RunBackHealthChecks(logger);

            if (Environment.UserInteractive)
            {
                var export = new ExportQueue(logger);
                export.Start();
                while (true)
                {
                    Console.WriteLine("Type 'exit' to stop the service...");
                    var input = Console.ReadLine();
                    if (input != null && input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
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
            Console.WriteLine("Set the appsettings_CrystalCmdWorkingFolder environment variable");
            Console.WriteLine("Each report that is generated will have its own subfolder inside the base working folder.");
            Console.WriteLine("Input files");
            Console.WriteLine("  BaseFolder/reporta/report.rpt");
            Console.WriteLine("  BaseFolder/reporta/report.json");
            Console.WriteLine("Output files");
            Console.WriteLine("  BaseFolder/reporta/report.pdf");
            Console.WriteLine("");
            Console.WriteLine(
                ".\\Majorsilence.CrystalCmd.Console.exe");
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