using Microsoft.Extensions.Logging;
using System;
using System.ServiceProcess;

namespace Majorsilence.CrystalCmd.NetframeworkConsole
{
    internal class WinService : ServiceBase
    {
        private readonly string _serviceName;
        private readonly ILogger _logger;
        ExportQueue export;
        public WinService(string serviceName, ILogger logger)
        {
            _serviceName = serviceName;
            ServiceName = serviceName;
            _logger = logger;
        }
        protected override void OnStart(string[] args)
        {
            // Start your service logic here
            export = new ExportQueue(_logger);
            export.Start();
            Console.WriteLine($"{_serviceName} started.");
        }
        protected override void OnStop()
        {
            // Clean up your service logic here
            export.Stop();
            Console.WriteLine($"{_serviceName} stopped.");
        }
    }
}