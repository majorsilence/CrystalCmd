using System;
using System.Collections.Generic;
using System.Text;

namespace Majorsilence.CrystalCmd.Common
{
    public class GetReportParametersRequest
    {
        public GetReportParametersRequest(string subreportName = null)
        {
            SubreportName = subreportName;
        }

        public string SubreportName { get; }
    }
}
