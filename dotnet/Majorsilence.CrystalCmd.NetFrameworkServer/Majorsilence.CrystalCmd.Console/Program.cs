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
        public static async Task Main(string[] args)
        {
            foreach (var t in args)
            {
                if (t == "/?" || t == "-help" || t == "help" || t == "--help")
                {
                    PrintHelp();
                    Environment.Exit(0);
                }
                else if (t.StartsWith("WorkingFolder=", StringComparison.OrdinalIgnoreCase))
                {
                    WorkingFolder = t.Substring("WorkingFolder=".Length);
                    Console.WriteLine($"Using working folder: {WorkingFolder}");
                }
            }

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();

            if (string.IsNullOrWhiteSpace(WorkingFolder))
            {
                WorkingFolder = Server.Common.Settings.GetSetting("CrystalCmdWorkingFolder");
                Console.WriteLine($"Using working folder from settings: {WorkingFolder}");
            }

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

                    var logger = _serviceProvider.GetService<ILogger>();
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
            var output = exporter.exportReportToStream(rptFile, item.Data);
            var bytes = output.Item1;
            var fileExt = output.Item2;
            var mimeType = output.Item3;

            try
            {
                System.IO.Directory.Delete(workingDir, true);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error deleting working folder {workingDir}: {ex.Message}");
            }

            return new GeneratedReportPoco
            {
                Id = item.Id,
                FileContent = bytes,
                Format = fileExt,
                Metadata = mimeType,
                FileName = $"{item.Id}.{fileExt}",
                GeneratedUtc = DateTime.UtcNow
            };
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