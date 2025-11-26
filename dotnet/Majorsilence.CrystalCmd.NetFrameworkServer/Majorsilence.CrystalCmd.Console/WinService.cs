using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ServiceProcess;

namespace Majorsilence.CrystalCmd.NetframeworkConsole
{
    internal class WinService : ServiceBase
    {
        private readonly string _serviceName;
        private readonly ILogger _logger;
        List<ExportQueue> exporters;
        ExportQueue analyzerExport;
        public WinService(string serviceName, ILogger logger)
        {
            _serviceName = serviceName;
            ServiceName = serviceName;
            _logger = logger;
        }
        protected override void OnStart(string[] args)
        {
            // Start your service logic here
            bool isSqlite = string.Equals(WorkQueue.GetSetting("WorkQueueSqlType"), "sqlite", StringComparison.InvariantCultureIgnoreCase);
            int threadCount = isSqlite ? 1 : Environment.ProcessorCount;
            exporters = ExportQueue.Create(_logger, "crystal-reports", threadCount);
            foreach (var exporter in exporters)
            {
                exporter.Start();
            }

            analyzerExport = new ExportQueue(_logger, "analyzer-reports");
            analyzerExport.Start();
            Console.WriteLine($"{_serviceName} started.");
        }
        protected override void OnStop()
        {
            // Clean up your service logic here
            foreach(var exporter in exporters)
            {
                exporter.Stop();
            }
            Console.WriteLine($"{_serviceName} stopped.");
        }
    }
}