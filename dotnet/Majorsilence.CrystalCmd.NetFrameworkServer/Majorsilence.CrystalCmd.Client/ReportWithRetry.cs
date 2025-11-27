using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    /// <summary>
    /// Will gracefully retry on http errors when polling for a report.
    /// </summary>
    public class ReportWithRetry : Report
    {
        [Obsolete("Use the constructor that takes a bearer token.")]
        public ReportWithRetry(string serverUrl = "https://c.majorsilence.com/export",
    string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0",
    string username = "", string password = "") : base(serverUrl, userAgent, username, password)
        {
        }

        public ReportWithRetry(string serverUrl = "https://c.majorsilence.com/export",
           string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0",
           string bearerToken = "") : base(serverUrl, userAgent, bearerToken)
        {
        }

        public new Stream Generate(Common.Data reportData, Stream report)
        {
            try
            {
                return base.Generate(reportData, report);
            }
            catch (HttpRequestException ex)
            {
                return base.Generate(reportData, report);
            }
        }

        public new Stream Generate(Common.Data reportData, Stream report, HttpClient httpClient)
        {
            try
            {
                return base.Generate(reportData, report, httpClient);
            }
            catch (HttpRequestException ex)
            {
                return base.Generate(reportData, report, httpClient);
            }
        }

        public new async Task<Stream> GenerateAsync(Common.Data reportData, Stream report,
            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {

            return await TryAsync(async () =>
            {
                return await base.GenerateAsync(reportData, report, cancellationToken);
            }, 3);
        }

        public new async Task<Stream> GenerateAsync(Common.Data reportData, Stream report, HttpClient httpClient,
            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {

            return await TryAsync(async () =>
             {
                 return await base.GenerateAsync(reportData, report, httpClient, cancellationToken);
             }, 3);
        }

        public async Task<T> TryAsync<T>(Func<Task<T>> actionToTry, int timesToRetry)
        {
            var errors = new List<HttpRequestException>();

            for (var time = 0; time < timesToRetry; time++)
            {
                try
                {
                    return await actionToTry();
                }
                catch (HttpRequestException ex)
                {
                    await Task.Delay(1000 * (time + 1));
                    errors.Add(ex);
                }
            }

            throw new AggregateException(errors);
        }

    }
}
