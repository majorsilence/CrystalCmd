using Majorsilence.CrystalCmd.Server;
using Majorsilence.CrystalCmd.Server.Common;
using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var queue = WorkQueue.CreateDefault();
            await queue.Migrate();

#if NET8_0_OR_GREATOR_WINDOWS
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddSingleton<StartupArgs>(new StartupArgs(args));
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "TownSuite Windows Service";
            });

            LoggerProviderOptions.RegisterProviderOptions<
                EventLogSettings, EventLogLoggerProvider>(builder.Services);

            builder.Services.AddHostedService<WebServerManager>();

            builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables();

            IHost host = builder.Build();
            host.Run();
#else
            // Run as console application
            var webServerManager = new WebServerManager();
            await webServerManager.StartAsync(CancellationToken.None);
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
            webServerManager.Stop();
#endif
        }
    }
}
