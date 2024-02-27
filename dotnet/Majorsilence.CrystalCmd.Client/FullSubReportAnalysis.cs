using System;
using System.Collections.Generic;
using System.Text;

namespace Majorsilence.CrystalCmd.Client
{
    public class FullSubReportAnalysis
    {
        internal FullSubReportAnalysis(string subreportName, IEnumerable<string> parameters, IEnumerable<DataTableAnalysis> dataTables)
        {
            SubreportName = subreportName;
            Parameters = parameters;
            DataTables = dataTables;
        }

        public string SubreportName { get; }
        public IEnumerable<string> Parameters { get; }
        public IEnumerable<DataTableAnalysis> DataTables { get; }

        public override string ToString()
        {
            var builder = new StringBuilder();
            var seperatorCount = 15;
            var seperator = new string('-', seperatorCount);
            builder.AppendLine("Subreport: " + SubreportName);
            builder.AppendLine(seperator);
            builder.AppendLine("Parameters: " + string.Join(", ", Parameters));
            builder.AppendLine(seperator);
            builder.AppendLine("Data Tables: " + string.Join(", ", DataTables));
            builder.AppendLine(seperator);
            return builder.ToString();
        }
    }
}
