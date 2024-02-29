using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Majorsilence.CrystalCmd.Client;

namespace Majorsilence.CrystalCmd.Server.Common
{
    public class CrystalReportsAnalyzer
    {

        public FullReportAnalysisResponse GetFullAnalysis(string reportPath)
        {
            using (var reportDocument = new ReportDocument())
            {
                reportDocument.Load(reportPath);

                return new FullReportAnalysisResponse()
                {
                    Parameters = GetReportParameters(reportDocument),
                    SubReports = GetSubreports(reportDocument),
                    DataTables = GetDataTables(reportDocument),
                };
            }
        }

        private IEnumerable<FullReportAnalysisResponse.FullSubReportAnalysisDto> GetSubreports(ReportDocument reportDocument)
        {
            var subreports = new List<FullReportAnalysisResponse.FullSubReportAnalysisDto>();
            foreach (ReportDocument subreport in reportDocument.Subreports)
            {
                subreports.Add(GetSubreport(reportDocument, subreport));
            }
            return subreports;
        }

        private FullReportAnalysisResponse.FullSubReportAnalysisDto GetSubreport(ReportDocument parentReport, ReportDocument subreport)
        {
            return new FullReportAnalysisResponse.FullSubReportAnalysisDto()
            {
                SubreportName = subreport.Name,
                Parameters = GetSubreportParameters(parentReport, subreport.Name),
                DataTables = GetDataTables(subreport)
            };
        }

        private IEnumerable<FullReportAnalysisResponse.DataTableAnalysisDto> GetDataTables(ReportDocument reportDocument)
        {
            var dataTables = new List<FullReportAnalysisResponse.DataTableAnalysisDto>();
            foreach (Table table in reportDocument.Database.Tables)
            {
                dataTables.Add(new FullReportAnalysisResponse.DataTableAnalysisDto()
                {
                    DataTableName = table.Name,
                    ColumnNames = GetDataTableColumns(table)
                });
            }
            return dataTables;
        }

        private IEnumerable<string> GetDataTableColumns(Table table)
        {
            var columns = new List<string>();
            foreach (DatabaseFieldDefinition column in table.Fields)
            {
                columns.Add(column.Name);
            }
            return columns;
        }

        private IEnumerable<string> GetSubreportParameters(ReportDocument reportDocument, string subreportName)
        {
            var parameters = new List<string>();
            foreach (ParameterField parameter in reportDocument.ParameterFields)
            {
                if (string.Equals(parameter.ReportName, subreportName))
                    parameters.Add(parameter.ParameterFieldName);
            }
            return parameters;
        }

        private IEnumerable<string> GetReportParameters(ReportDocument reportDocument)
        {
            var parameters = new List<string>();
            foreach (ParameterField parameter in reportDocument.ParameterFields)
            {
                if (string.IsNullOrEmpty(parameter.ReportName))
                    parameters.Add(parameter.ParameterFieldName);
            }
            return parameters;
        }
    }
}
