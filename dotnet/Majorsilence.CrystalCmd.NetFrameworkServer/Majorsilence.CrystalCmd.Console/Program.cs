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
            

            string baseFolder = Server.Common.Settings.GetSetting("CrystalCmdWorkingFolder");
            var dataQueue = new ConcurrentQueue<(string, string, string)>();
            while (true)
            {
                foreach(var report in FindFileToProcess(baseFolder))
                {
                    dataQueue.Enqueue(report);
                }
                
                // permit a maximum of 5 reports processing at any given time
                int numTasks = dataQueue.Count > 5 ? 5 : dataQueue.Count;

                if (numTasks > 0)
                {
                    Task[] tasks = new Task[numTasks];
                    for (int i = 0; i < numTasks; i++)
                    {
                        tasks[i] = Task.Run(() => ProcessData(dataQueue));
                    }
                    
                    Task.WaitAll(tasks);
                }
                
                await Task.Delay(1000);
            }
        }
        
        static void ProcessData(ConcurrentQueue<(string RptFile, string DataFile, string WorkingDir)> dataQueue)
        {
            while (!dataQueue.IsEmpty)
            {
                if (dataQueue.TryDequeue(out (string RptFile, string DataFile, string WorkingDir) dataItem))
                {
                    Console.WriteLine($"Processing {dataItem} on thread {Thread.CurrentThread.ManagedThreadId}");

                    var logger = _serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger>();
                    var exporter = new Majorsilence.CrystalCmd.Server.Common.Exporter(logger);
                    var reportData = Newtonsoft.Json.JsonConvert.DeserializeObject<CrystalCmd.Common.Data>(System.IO.File.ReadAllText(dataItem.DataFile));
                    var output = exporter.exportReportToStream(dataItem.RptFile, reportData);
                    var bytes = output.Item1;
                    var fileExt = output.Item2;
                    var mimeType = output.Item3;
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(dataItem.WorkingDir, $"report.{fileExt}") , bytes);
                }
            }
        }
        
        private static IEnumerable<(string RptFile, string DataFile, string WorkingDir)> FindFileToProcess(string baseFolder)
        {
            var dInfo = new DirectoryInfo(baseFolder);
            var foundDirectories = dInfo.GetDirectories().OrderBy(p=>p.CreationTimeUtc);

            foreach (var dir in foundDirectories)
            {
                var rptFile = dir.GetFiles("*.rpt").FirstOrDefault();
                var dataFile = dir.GetFiles("*.json").FirstOrDefault();
                var completedFile = dir.GetFiles("completed.txt")?.FirstOrDefault();
                var startedFile = dir.GetFiles("started.txt")?.FirstOrDefault();
                
                if (completedFile != null)
                {
                    // already processed
                    Console.WriteLine($"Skipping value {dir} that finished processing.");
                    continue;
                }
                if (startedFile != null)
                {
                    // already processing
                    Console.WriteLine($"Skipping value {dir} that started processing.");
                    continue;
                }
                
                yield return (rptFile.FullName, dataFile.FullName, dir.FullName);
            }
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
            services.AddLogging(configure => {
                configure.ClearProviders();
                configure.AddConsole();
            })
            .Configure<LoggerFilterOptions>(options => options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Information);

            services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(s => {
                return s.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("CrystalCmd");
            });
        }
    }
}