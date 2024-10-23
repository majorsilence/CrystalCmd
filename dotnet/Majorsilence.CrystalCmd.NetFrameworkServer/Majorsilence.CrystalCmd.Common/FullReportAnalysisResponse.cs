using System;
using System.Collections.Generic;

namespace Majorsilence.CrystalCmd.Common
{
    public class FullReportAnalysisResponse
    {
        public FullReportAnalysisResponse()
        {
            Parameters = new string[0];
            DataTables = new DataTableAnalysisDto[0];
            SubReports = new FullSubReportAnalysisDto[0];
            ReportObjects = new ReportObjectsDto[0];
        }

        public IEnumerable<string> Parameters { get; set; }
        public IDictionary<string, string> ParametersExtended { get; set; }
        public IEnumerable<DataTableAnalysisDto> DataTables { get; set; }
        public IEnumerable<FullSubReportAnalysisDto> SubReports { get; set; }
        public IEnumerable<ReportObjectsDto> ReportObjects { get; set; }

        public class FullSubReportAnalysisDto
        {
            public FullSubReportAnalysisDto()
            {
                Parameters = new string[0];
                DataTables = new DataTableAnalysisDto[0];
            }

            public string SubreportName { get; set; }
            public IEnumerable<string> Parameters { get; set; }
            public IEnumerable<DataTableAnalysisDto> DataTables { get; set; }
        }

        public class DataTableAnalysisDto
        {
            public DataTableAnalysisDto()
            {
                ColumnNames = new string[0];
            }

            public string DataTableName { get; set; }
            public IEnumerable<string> ColumnNames { get; set; }
        }

        public class ReportObjectsDto
        {
            public string ObjectName { get; set; }
            public int Width { get; set; }
            public int TopPosition { get; set; }
            public string ObjectValue { get; set; }
        }
    }
}
