using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Majorsilence.CrystalCmd.Server.Common;
using System.ServiceProcess;
using System.Threading;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                // Run as console application
                var webServerManager = new WebServerManager();
                await webServerManager.StartAsync(CancellationToken.None);
                Console.WriteLine("Press Enter to exit...");
                Console.ReadLine();
                webServerManager.Stop();
            }
            else
            {
                // Run as Windows Service
                string serviceName = Settings.GetSetting("ServiceName");
                if (string.IsNullOrWhiteSpace(serviceName))
                {
                    serviceName = "CrystalCmdService";
                }
                ServiceBase.Run(new WebService(serviceName));
            }
        }
    }
}
