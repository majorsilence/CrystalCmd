using System;
using System.Collections.Generic;
using System.Text;

namespace Majorsilence.CrystalCmd.Common
{
    public class SubReportParameters
    {
        public string ReportName { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }
}
