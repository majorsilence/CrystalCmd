using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    public class Report : IReport
    {
        readonly string serverUrl;
        readonly string userAgent;
        readonly string username;
        readonly string password;
        public Report(string serverUrl = "https://c.majorsilence.com/export",
            string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0",
            string username = "", string password = "")
        {
            this.serverUrl = serverUrl;
            this.userAgent = userAgent;
            this.username = username;
            this.password = password;
        }

        public Stream Generate(Data reportData, Stream report)
        {
            return System.Threading.Tasks.Task.Run(async () => await GenerateAsync(reportData, report,
                System.Threading.CancellationToken.None)).GetAwaiter().GetResult();
        }

        public Stream Generate(Data reportData, Stream report, HttpClient httpClient)
        {
            return System.Threading.Tasks.Task.Run(async () => await GenerateAsync(reportData, report, httpClient,
                System.Threading.CancellationToken.None)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Call a server with a crystal report template and data and have a pdf report returned.
        /// An HttpClient is created disposed on every call to this method.  It is recommended
        /// to call the GenerateAsync method passing in an HttpClient.
        /// </summary>
        /// <param name="reportData"></param>
        /// <param name="report"></param>
        /// <returns></returns>
        /// <example>
        /// <code lang="cs">
        /// using (var fstream = new FileStream("thereport.rpt", FileMode.Open))
        /// using (var fstreamOut = new FileStream("thereport.pdf", FileMode.OpenOrCreate | FileMode.Append))
        /// {
        ///     var rpt = new Majorsilence.CrystalCmd.Client.Report();
        ///     using (var stream = await rpt.GenerateAsync(new Data(), fstream))
        ///     {
        ///         stream.CopyTo(fstreamOut);
        ///     }
        /// }
        ///</code>
        /// </example>
        public async Task<Stream> GenerateAsync(Data reportData, Stream report,
            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {
            using (var httpClient = new HttpClient())
            {
                return await GenerateAsync(reportData, report, httpClient, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Call a server with a crystal report template and data and have a pdf report returned.
        /// Is the recommended method for HttpClient performance reasons.
        /// </summary>
        /// <param name="reportData"></param>
        /// <param name="report"></param>
        /// <param name="httpClient">Manage the HttpClient from the calling program.</param>
        /// <returns></returns>
        /// <example>
        /// <code lang="cs">
        /// using (var fstream = new FileStream("thereport.rpt", FileMode.Open))
        /// using (var fstreamOut = new FileStream("thereport.pdf", FileMode.OpenOrCreate | FileMode.Append))
        /// {
        ///     var rpt = new Majorsilence.CrystalCmd.Client.Report();
        ///     using (var stream = await rpt.GenerateAsync(new Data(), fstream, new HttpClient()))
        ///     {
        ///         stream.CopyTo(fstreamOut);
        ///     }
        /// }
        ///</code>
        /// </example>
        public Task<Stream> GenerateAsync(Data reportData, Stream report, HttpClient httpClient,
            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {
            var serverCaller = new ServerCaller(httpClient, serverUrl);
            return serverCaller.GenerateAsync(reportData, report, "", username, password, userAgent, cancellationToken);
        }
    }
}
