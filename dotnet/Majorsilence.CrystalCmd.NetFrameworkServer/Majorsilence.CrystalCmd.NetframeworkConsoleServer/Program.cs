using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Configuration;
using System.Runtime.Remoting.Contexts;
using System.Security.AccessControl;
using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.Files;
using Majorsilence.CrystalCmd.Server.Common;
using Swan.Logging;
using System.Runtime.Remoting.Messaging;
using System.Net.Mime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Web;
using System.Web.Hosting;
using NReco.Logging.File;
using System.Reflection;
using Majorsilence.CrystalCmd.Common;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
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
