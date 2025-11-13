using ChoETL;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server.Common
{
    public class CrystalReportsAnalyzer
    {

        public CrystalCmd.Common.FullReportAnalysisResponse GetFullAnalysis(string reportPath)
        {
            using (var reportDocument = new ReportDocument())
            {
                reportDocument.Load(reportPath);

                return new CrystalCmd.Common.FullReportAnalysisResponse()
                {
                    Parameters = GetReportParameters(reportDocument),
                    ParametersExtended = GetReportParametersExtended(reportDocument),
                    SubReports = GetSubreports(reportDocument),
                    DataTables = GetDataTables(reportDocument),
                    ReportObjects = GetReportObjects(reportDocument)
                };
            }
        }

        public CrystalCmd.Common.FullReportAnalysisResponse GetFullAnalysis(byte[] reportTemplate)
        {
            string workingId = Guid.NewGuid().ToString();

            string workingDir = System.IO.Path.Combine(Server.Common.WorkingFolder.GetMajorsilenceTempFolder(), workingId);
            System.IO.Directory.CreateDirectory(workingDir);
            string rptFile = System.IO.Path.Combine(workingDir, $"{workingId}.rpt");
            System.IO.File.WriteAllBytes(rptFile, reportTemplate);

            CrystalCmd.Common.FullReportAnalysisResponse result;
            using (var reportDocument = new ReportDocument())
            {
                reportDocument.Load(rptFile);

                result = new CrystalCmd.Common.FullReportAnalysisResponse()
                {
                    Parameters = GetReportParameters(reportDocument),
                    ParametersExtended = GetReportParametersExtended(reportDocument),
                    SubReports = GetSubreports(reportDocument),
                    DataTables = GetDataTables(reportDocument),
                    ReportObjects = GetReportObjects(reportDocument)
                };

                reportDocument.Close();
            }

            try
            {
                System.IO.Directory.Delete(workingDir, true);
            }
            catch
            {
                // Do nothing if we can't delete temp files, they will be cleaned up later.
            }

            return result;
        }

        private IEnumerable<CrystalCmd.Common.FullReportAnalysisResponse.FullSubReportAnalysisDto> GetSubreports(ReportDocument reportDocument)
        {
            var subreports = new List<CrystalCmd.Common.FullReportAnalysisResponse.FullSubReportAnalysisDto>();
            foreach (ReportDocument subreport in reportDocument.Subreports)
            {
                subreports.Add(GetSubreport(reportDocument, subreport));
            }
            return subreports;
        }

        private CrystalCmd.Common.FullReportAnalysisResponse.FullSubReportAnalysisDto GetSubreport(ReportDocument parentReport, ReportDocument subreport)
        {
            return new CrystalCmd.Common.FullReportAnalysisResponse.FullSubReportAnalysisDto()
            {
                SubreportName = subreport.Name,
                Parameters = GetSubreportParameters(parentReport, subreport.Name),
                DataTables = GetDataTables(subreport)
            };
        }

        private IEnumerable<CrystalCmd.Common.FullReportAnalysisResponse.DataTableAnalysisDto> GetDataTables(ReportDocument reportDocument)
        {
            var dataTables = new List<CrystalCmd.Common.FullReportAnalysisResponse.DataTableAnalysisDto>();
            foreach (Table table in reportDocument.Database.Tables)
            {
                dataTables.Add(new CrystalCmd.Common.FullReportAnalysisResponse.DataTableAnalysisDto()
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

        private Dictionary<string, string> GetReportParametersExtended(ReportDocument reportDocument)
        {
            var parameters = new Dictionary<string, string>();
            foreach (ParameterField parameter in reportDocument.ParameterFields)
            {
                if (string.IsNullOrEmpty(parameter.ReportName))
                    parameters.Add(parameter.ParameterFieldName, parameter.ParameterValueType.ToString());
            }
            return parameters;
        }

        private IEnumerable<CrystalCmd.Common.FullReportAnalysisResponse.ReportObjectsDto> GetReportObjects(ReportDocument reportDocument)
        {
            var reportObjects = new List<CrystalCmd.Common.FullReportAnalysisResponse.ReportObjectsDto>();

            foreach (Section section in reportDocument.ReportDefinition.Sections)
            {
                foreach (ReportObject reportObject in section.ReportObjects)
                {
                    reportObjects.Add(new CrystalCmd.Common.FullReportAnalysisResponse.ReportObjectsDto()
                    {
                        ObjectName = reportObject.Name,
                        Width = reportObject.Width,
                        TopPosition = reportObject.Top,
                        ObjectValue = reportObject is TextObject ? (reportObject as TextObject).Text : reportObject.ToString()
                    });
                }
            }
            return reportObjects;
        }
    }
}
