using Majorsilence.CrystalCmd.Server.Common;
using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Threading;
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

            if (string.IsNullOrWhiteSpace(WorkingFolder))
            {

                Console.WriteLine($"Using working folder from settings: {WorkingFolder}");
            }

            var logger = _serviceProvider.GetService<ILogger>();
            string runndingDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string rptPath = System.IO.Path.Combine(runndingDir, "thereport.rpt");

            _backgroundHealthTask = new HealthCheckTask(logger, rptPath, true);
            _backgroundHealthTask.Start();

            var queue = WorkQueue.CreateDefault();
            await queue.Migrate();

            string baseFolder = WorkingFolder;
            while (true)
            {
                bool processed = false;

                try
                {
                    await queue.Dequeue(async (item) =>
                    {
                        var report = await ProcessData(item.PayloadAsQueueItem, queue);
                        processed = true;
                        return report;
                    });
                }
                catch (Exception ex)
                {


                    logger.LogError($"Error processing queue item: {ex.Message}");
                }


                if (!processed)
                {
                    // No items to process, wait a bit
                    await Task.Delay(1000);
                    continue;
                }
            }
        }

        static async Task<GeneratedReportPoco> ProcessData(QueueItem item, WorkQueue queue)
        {
            var logger = _serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>();
            var exporter = new Majorsilence.CrystalCmd.Server.Common.Exporter(logger);

            string workingDir = System.IO.Path.Combine(Server.Common.WorkingFolder.GetMajorsilenceTempFolder(), item.Id);
            System.IO.Directory.CreateDirectory(workingDir);
            string rptFile = System.IO.Path.Combine(workingDir, $"{item.Id}.rpt");
            System.IO.File.WriteAllBytes(rptFile, item.ReportTemplate);

            GeneratedReportPoco poco = null;

            if (item.Data != null)
            {
                // export pdf

                var output = exporter.exportReportToStream(rptFile, item.Data);
                var bytes = output.Item1;
                var fileExt = output.Item2;
                var mimeType = output.Item3;
                poco = new GeneratedReportPoco
                {
                    Id = item.Id,
                    FileContent = bytes,
                    Format = fileExt,
                    Metadata = mimeType,
                    FileName = $"{item.Id}.{fileExt}",
                    GeneratedUtc = DateTime.UtcNow
                };
            }
            else
            {
                // report analysis
                var analyzer = new CrystalReportsAnalyzer();
                var response = analyzer.GetFullAnalysis(rptFile);
                return new GeneratedReportPoco
                {
                    Id = item.Id,
                    GeneratedUtc = DateTime.UtcNow,
                    FileName = $"{item.Id}_analysis.json",
                    Format = "json",
                    FileContent = System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(response)),
                    Metadata = "application/json"
                };
            }

            try
            {
                System.IO.Directory.Delete(workingDir, true);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error deleting working folder {workingDir}: {ex.Message}");
            }

            return poco;
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