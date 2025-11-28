using Majorsilence.CrystalCmd.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    [Obsolete("Use the Report class directly.")]
    public class ReportWithPolling : Report
    {
        public ReportWithPolling(string serverUrl = "https://c.majorsilence.com",
    string userAgent = "Majorsilence.CrystalCmd.Client/1.0.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0",
    string bearerToken = "")
    : base(serverUrl, userAgent, bearerToken)
        {
        }

        [Obsolete("Use the constructor that takes a bearer token.")]
        public ReportWithPolling(string serverUrl, string userAgent, string username, string password)
            : base(serverUrl, userAgent, username, password)
        {
        }
    }
}
