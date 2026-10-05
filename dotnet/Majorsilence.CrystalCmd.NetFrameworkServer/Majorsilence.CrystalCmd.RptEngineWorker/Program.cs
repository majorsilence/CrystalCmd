using Majorsilence.CrystalCmd.RptEngine;
using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// The CrystalCmd queue worker on Majorsilence.Crystal's engine. It consumes its own
// channels (rptengine-reports, rptengine-analyzer by default), which the API routes
// serviceable requests to; the Crystal Reports worker keeps its channels and everything
// the rule sends there. Same work queue, same database, same request contract.

if (args.Any(a => a is "/?" or "-help" or "help" or "--help"))
{
    Console.WriteLine("Majorsilence.CrystalCmd.RptEngineWorker");
    Console.WriteLine("Configure the work queue in appsettings.json or with the environment variables");
    Console.WriteLine("WorkQueue__SqlType (sqlite, mssql, psql) and WorkQueue__SqlConnection, the same keys the server uses.");
    Console.WriteLine("Worker__ReportsChannel and Worker__AnalyzerChannel override the queue channels.");
    Console.WriteLine("Worker__PrinterPaper (Letter, A4 or Legal) is the paper the Crystal host's printer holds,");
    Console.WriteLine("for templates that print on their printer's default paper.");
    return 0;
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

using var loggerFactory = LoggerFactory.Create(builder => builder
    .AddConsole()
    .SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger("CrystalCmd.RptEngineWorker");

string reportsChannel = configuration["Worker:ReportsChannel"] ?? RptEngineWorkerChannels.Reports;
string analyzerChannel = configuration["Worker:AnalyzerChannel"] ?? RptEngineWorkerChannels.Analyzer;

// SQLite takes one writer at a time, so one loop; the server databases take one per core.
bool isSqlite = string.Equals(configuration["WorkQueue:SqlType"], "sqlite", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(configuration["WorkQueue:SqlType"]);
int threadCount = isSqlite ? 1 : Environment.ProcessorCount;

string workingRoot = Path.Combine(Path.GetTempPath(), "majorsilence", "crystalcmd-rptengine");
Directory.CreateDirectory(workingRoot);

// The paper a template that names none prints on, in Crystal: its printer's. A wrong value
// stops the worker here rather than being ignored.
var printerPaper = RptEngineExporter.ParsePrinterPaper(configuration["Worker:PrinterPaper"]);
logger.LogInformation("Templates that print on their printer's default paper are laid out on {Paper}",
    printerPaper is null ? "the page they were designed on" : configuration["Worker:PrinterPaper"]!.Trim());

var exporter = new RptEngineExporter(logger, printerPaper);
var analyzer = new RptEngineAnalyzer();

var health = new BackendHealthCheck(logger, exporter, Path.Combine(AppContext.BaseDirectory, "thereport.rpt"),
    failureShouldExitProcess: true);
health.Start();

var processors = new List<QueueProcessor>();
for (int i = 0; i < threadCount; i++)
    processors.Add(new QueueProcessor(logger, reportsChannel, exporter, analyzer,
        () => WorkQueue.CreateDefault(reportsChannel, configuration), workingRoot));
processors.Add(new QueueProcessor(logger, analyzerChannel, exporter, analyzer,
    () => WorkQueue.CreateDefault(analyzerChannel, configuration), workingRoot));

foreach (var processor in processors)
    processor.Start();
logger.LogInformation("Rendering with Majorsilence.Crystal on channels {Reports} and {Analyzer} with {Threads} report loop(s); press ctrl+c to stop",
    reportsChannel, analyzerChannel, threadCount);

var stopping = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.TrySetResult(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => stopping.TrySetResult();
await stopping.Task;

foreach (var processor in processors)
    processor.Stop();
health.Stop();
return 0;
