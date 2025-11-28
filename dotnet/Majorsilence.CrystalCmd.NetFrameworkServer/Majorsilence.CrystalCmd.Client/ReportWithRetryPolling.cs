using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    [Obsolete("Use ReportWithRetry instead.")]
    public class ReportWithRetryPolling : ReportWithRetry
    {
        [Obsolete("Use the constructor that takes a bearer token.")]
        public ReportWithRetryPolling(string serverUrl = "https://c.majorsilence.com",
    string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0",
    string username = "", string password = "") : base(serverUrl, userAgent, username, password)
        {
        }

        public ReportWithRetryPolling(string serverUrl = "https://c.majorsilence.com",
           string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0",
           string bearerToken = "") : base(serverUrl, userAgent, bearerToken)
        {
        }
    }
}
