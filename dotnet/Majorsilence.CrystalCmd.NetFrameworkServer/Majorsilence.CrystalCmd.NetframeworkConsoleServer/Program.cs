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
using Majorsilence.CrystalCmd.Client;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    internal class Program
    {

        static int port = 44355;

        static async Task Main(string[] args)
        {
            
            WebServer ws;
            Dictionary<string, List<string>> ends = GetAllEndPoints();
            ws = new WebServer(ends, SendResponse);
            ws.Run();
            Console.WriteLine($"Port {port}");

            while (true)
            {
                await Task.Delay(1000);
            }
        }

        static async Task<HttpResponseMessage>
           SendResponse(string rawurl, System.Collections.Specialized.NameValueCollection headers,
             Stream inputStream,
             System.Text.Encoding inputContentEncoding,
             string contentType
           )
        {
            if (string.Equals(rawurl, "/status", StringComparison.InvariantCultureIgnoreCase))
            {
              //  return (200, "I'm alive", "text/plain; charset=UTF-8", inputContentEncoding);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("I'm alive")
                };
            }
            else if (string.Equals(rawurl, "/export", StringComparison.InvariantCultureIgnoreCase))
            {
                var creds = UsernameAndPassword(headers);
                string user = ConfigurationManager.AppSettings["Username"];
                string password = ConfigurationManager.AppSettings["Password"];
                var expected_creds = Tuple.Create(user, password);
                var callResult = Authenticate(creds, expected_creds);
                if (callResult.StatusCode != 200)
                    return new HttpResponseMessage((HttpStatusCode)callResult.StatusCode)
                    {
                        Content = new StringContent(callResult.message)
                    };

                var streamContent = new StreamContent(inputStream);
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

                var provider = await streamContent.ReadAsMultipartAsync();
                Client.Data reportData = null;
                byte[] reportTemplate = null;

                foreach (var file in provider.Contents)
                {
                    // https://stackoverflow.com/questions/7460088/reading-file-input-from-a-multipart-form-data-post
                    string name = file.Headers.ContentDisposition.Name.Replace("\"", "");
                    if (string.Equals(name, "reportdata", StringComparison.CurrentCultureIgnoreCase))
                    {
                        reportData = Newtonsoft.Json.JsonConvert.DeserializeObject<Client.Data>(await file.ReadAsStringAsync());
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
                    reportPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid().ToString()}.rpt");
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
                    var message = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent(ex.Message + System.Environment.NewLine + ex.StackTrace)
                    };

                    return message;
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

                var result = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes)
                };
                result.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = "report.pdf"
                };
                result.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                return result;

            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("400")
            };
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
