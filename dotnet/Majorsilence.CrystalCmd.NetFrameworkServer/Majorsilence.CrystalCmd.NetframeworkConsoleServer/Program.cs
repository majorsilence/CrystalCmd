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
using Majorsilence.CrystalCmd.Common;
using System.Security.AccessControl;
using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.Files;
using Majorsilence.CrystalCmd.Server.Common;
using Swan.Logging;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    internal class Program
    {

        static int port = 44355;

        static async Task Main(string[] args)
        {
            string workingfolder = WorkingFolder.GetMajorsilenceTempFolder();
            if (System.IO.Directory.Exists(workingfolder))
            {
                System.IO.Directory.Delete(workingfolder, true);
            }
            System.IO.Directory.CreateDirectory(workingfolder);

            var url = $"http://*:{port}/";

            // Our web server is disposable.
            using (var server = CreateWebServer(url))
            {
                // Once we've registered our modules and configured them, we call the RunAsync() method.
                await server.RunAsync();
                
                while (true)
                {
                    await Task.Delay(1000);
                }
            }
        }
        
        // Create and configure our web server.
        private static WebServer CreateWebServer(string url)
        {
            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                .WithModule(new ActionModule("/status", HttpVerbs.Any, async (ctx) =>
                    {
                         await SendResponse( "/status", ctx.Request.Headers, ctx.Request.InputStream,
                            ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx); 
                    }))
                .WithModule(new ActionModule("/export", HttpVerbs.Any, async (ctx) =>
                {
                    await SendResponse( "/export", ctx.Request.Headers, ctx.Request.InputStream,
                        ctx.Request.ContentEncoding, ctx.Request.ContentType, ctx); 
                }));

            // Listen for state changes.
            server.StateChanged += (s, e) => $"WebServer New State - {e.NewState}".Info();

            return server;
        }

        static async Task
           SendResponse(string rawurl, System.Collections.Specialized.NameValueCollection headers,
             Stream inputStream,
             System.Text.Encoding inputContentEncoding,
             string contentType,
             IHttpContext ctx
           )
        {
            if (string.Equals(rawurl, "/status", StringComparison.InvariantCultureIgnoreCase))
            {
              //  return (200, "I'm alive", "text/plain; charset=UTF-8", inputContentEncoding);
                ctx.SendStringAsync("I'm alive", "text/plain", Encoding.UTF8);
                return;
            }
            else if (string.Equals(rawurl, "/export", StringComparison.InvariantCultureIgnoreCase))
            {
                var creds = UsernameAndPassword(headers);
                string user = Settings.GetSetting("Username");
                string password = Settings.GetSetting("Password");
                var expected_creds = Tuple.Create(user, password);
                var callResult = Authenticate(creds, expected_creds);
                if (callResult.StatusCode != 200)
                {
                    ctx.Response.StatusCode = callResult.StatusCode;
                    return;
                }

                var streamContent = new StreamContent(inputStream);
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

                var provider = await streamContent.ReadAsMultipartAsync();
                ReportData reportData = null;
                byte[] reportTemplate = null;

                foreach (var file in provider.Contents)
                {
                    // https://stackoverflow.com/questions/7460088/reading-file-input-from-a-multipart-form-data-post
                    string name = file.Headers.ContentDisposition.Name.Replace("\"", "");
                    if (string.Equals(name, "reportdata", StringComparison.CurrentCultureIgnoreCase))
                    {
                        reportData = Newtonsoft.Json.JsonConvert.DeserializeObject<ReportData>(await file.ReadAsStringAsync());
                    }
                    else
                    {
                        reportTemplate = await file.ReadAsByteArrayAsync();
                    }
                }

                string reportPath = null;
                byte[] bytes = null;
                try
                {
                    reportPath = Path.Combine(WorkingFolder.GetMajorsilenceTempFolder(), $"{Guid.NewGuid().ToString()}.rpt");
                    // System.IO.File.WriteAllBytes(reportPath, reportTemplate);
                    // Using System.IO.File.WriteAllBytes randomly causes problems where the system still 
                    // has the file open when crystal attempts to load it and crystal fails.
                    using (var fstream = new FileStream(reportPath, FileMode.Create))
                    {
                        await fstream.WriteAsync(reportTemplate, 0, reportTemplate.Length);
                        await fstream.FlushAsync();
                        fstream.Close();
                    }

                    var exporter = new Majorsilence.CrystalCmd.Server.Common.PdfExporter();
                    bytes = exporter.exportReportToStream(reportPath, reportData);
                    
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);

                    ctx.Response.StatusCode = 500;
                    return;
                }
                finally
                {
                    try
                    {
                        System.IO.File.Delete(reportPath);
                    }
                    catch (Exception)
                    {
                        // TODO: cleanup will happen later
                    }
                }
                
                // Set the headers for content disposition and content type
                ctx.Response.Headers.Add("Content-Disposition", "attachment; filename=report.pdf");
                ctx.Response.ContentType = "application/octet-stream";
                ctx.Response.ContentLength64 = bytes.Length;
                ctx.Response.StatusCode = 200;

                // Write the byte array to the response stream
                await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                await ctx.Response.OutputStream.FlushAsync();

                // Ensure that all headers and content are sent
                ctx.Response.Close();
                return;
            }
            ctx.Response.StatusCode = 400;
        }

        private static Tuple<string, string> UsernameAndPassword(NameValueCollection headers)
        {
            string authHeader = headers["Authorization"];
            if (authHeader != null && authHeader.StartsWith("Basic"))
            {
                //Extract credentials
                string encodedUsernamePassword = authHeader.Substring("Basic ".Length).Trim();
                //the coding should be iso or you could use ASCII and UTF-8 decoder
                Encoding encoding = Encoding.GetEncoding("iso-8859-1");
                string usernamePassword = encoding.GetString(Convert.FromBase64String(encodedUsernamePassword));

                int seperatorIndex = usernamePassword.IndexOf(':');

                string username = usernamePassword.Substring(0, seperatorIndex);
                string password = usernamePassword.Substring(seperatorIndex + 1);

                return Tuple.Create(username, password);
            }
            return null;
        }

        static (int StatusCode, string message) Authenticate(Tuple<string, string> credentials, Tuple<string, string> expected_credentials)
        {
            if (credentials?.Item1 != expected_credentials.Item1 || credentials?.Item2 != expected_credentials.Item2)
            {
                return (401, "Unauthorized");
            }
            return (200, "");
        }

        
        static Dictionary<string, List<string>> GetAllEndPoints()
        {
            Dictionary<string, List<string>> listofends = new Dictionary<string, List<string>>();
           // listofends.Add("0.0.0.0", AddEndpoints("0.0.0.0"));

            // Get a list of all network interfaces (usually one per network card, dialup, and VPN connection)
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface network in networkInterfaces)
            {
                // Read the IP configuration for each network
                IPInterfaceProperties properties = network.GetIPProperties();

                // Each network interface may have multiple IP addresses
                foreach (IPAddressInformation address in properties.UnicastAddresses)
                {
                    if (address.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
                    {
                        string ipaddress = address.Address.ToString();
                        listofends.Add(ipaddress, AddEndpoints(ipaddress));
                    }
                }
            }

            return listofends;
        }

        private static List<string> AddEndpoints(string address)
        {
            List<string> listofends = new List<string>();
            listofends.Add("http://" + address + $":{port}/status/");
            listofends.Add("http://" + address + $":{port}/export/");

            return listofends;
        }
    }

}
