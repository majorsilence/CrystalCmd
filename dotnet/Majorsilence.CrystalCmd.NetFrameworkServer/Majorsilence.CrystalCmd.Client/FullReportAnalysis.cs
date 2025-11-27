using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Majorsilence.CrystalCmd.Client
{
    public class FullReportAnalysis
    {
        internal FullReportAnalysis(IEnumerable<string> parameters, IEnumerable<DataTableAnalysis> dataTables, IEnumerable<FullSubReportAnalysis> subReports,
            IEnumerable<Common.FullReportAnalysisResponse.ReportObjectsDto> reportObjects)
        {
            Parameters = parameters;
            DataTables = dataTables;
            SubReports = subReports;
            ReportObjects = reportObjects;
        }

        public IEnumerable<FullSubReportAnalysis> SubReports { get; }
        public IEnumerable<string> Parameters { get; }
        public IEnumerable<DataTableAnalysis> DataTables { get; }
        public IEnumerable<Common.FullReportAnalysisResponse.ReportObjectsDto> ReportObjects { get; set; }

        public bool HasSubReport() => SubReports?.Any() ?? false;

        public override string ToString()
        {
            var builder = new StringBuilder();
            var seperatorCount = 15;
            var headerSeperator = new string('=', seperatorCount);
            var seperator = new string('-', seperatorCount);
            builder.AppendLine(headerSeperator);
            builder.AppendLine("Report Analysis");
            builder.AppendLine(headerSeperator);
            builder.AppendLine("Parameters: " + string.Join(", ", Parameters));
            builder.AppendLine(seperator);
            builder.AppendLine("Data Tables: " + string.Join(", ", DataTables));
            builder.AppendLine(seperator);
            builder.AppendLine("Sub Reports: " + string.Join(", ", SubReports));
            builder.AppendLine(headerSeperator);
            return builder.ToString();
        }
    }
}
