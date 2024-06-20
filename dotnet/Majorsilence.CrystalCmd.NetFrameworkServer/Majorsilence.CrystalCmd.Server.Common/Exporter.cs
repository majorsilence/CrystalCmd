using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI.WebControls;
using System.Windows.Documents;
using System.Xml.Linq;
using ChoETL;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using Microsoft.Extensions.Logging;


namespace Majorsilence.CrystalCmd.Server.Common
{
    public class Exporter
    {
        private readonly ILogger _logger;
        public Exporter(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reportPath"></param>
        /// <param name="datafile"></param>
        /// <returns>byte array, file extension, mimetype</returns>
        public Tuple<byte[], string, string> exportReportToStream(string reportPath, CrystalCmd.Common.Data datafile)
        {
            var crystalWrapper = new CrystalDocumentWrapper(_logger);
            using (var reportClientDocument = crystalWrapper.Create(reportPath, datafile))
            {
                return Export(datafile.ExportAs, reportClientDocument);
            }
        }

        private Tuple<byte[], string, string> Export(CrystalCmd.Common.ExportTypes expFormatType, ReportDocument rpt)
        {
            CrystalDecisions.Shared.ExportFormatType exp;
            string fileExt;
            string mimetype;

            switch (expFormatType)
            {
                case CrystalCmd.Common.ExportTypes.CSV:
                    exp = CrystalDecisions.Shared.ExportFormatType.CharacterSeparatedValues;
                    fileExt = "csv";
                    mimetype = "text/csv";
                    break;
                case CrystalCmd.Common.ExportTypes.CrystalReport:
                    exp = CrystalDecisions.Shared.ExportFormatType.CrystalReport;
                    fileExt = "rpt";
                    mimetype = "application/octet-stream";
                    break;
                case CrystalCmd.Common.ExportTypes.Excel:
                    exp = CrystalDecisions.Shared.ExportFormatType.Excel;
                    fileExt = "xls";
                    mimetype = "application/vnd.ms-excel";
                    break;
                case CrystalCmd.Common.ExportTypes.ExcelDataOnly:
                    exp = CrystalDecisions.Shared.ExportFormatType.ExcelRecord;
                    fileExt = "xls";
                    mimetype = "application/vnd.ms-excel";
                    break;
                case CrystalCmd.Common.ExportTypes.PDF:
                    exp = CrystalDecisions.Shared.ExportFormatType.PortableDocFormat;
                    fileExt = "pdf";
                    mimetype = "application/pdf";
                    break;
                case CrystalCmd.Common.ExportTypes.RichText:
                    exp = CrystalDecisions.Shared.ExportFormatType.RichText;
                    fileExt = "rtf";
                    mimetype = "application/rtf";
                    break;
                case CrystalCmd.Common.ExportTypes.TEXT:
                    exp = CrystalDecisions.Shared.ExportFormatType.Text;
                    fileExt = "txt";
                    mimetype = "text/plain";
                    break;
                case CrystalCmd.Common.ExportTypes.WordDoc:
                    exp = CrystalDecisions.Shared.ExportFormatType.WordForWindows;
                    fileExt = "doc";
                    mimetype = "application/msword";
                    break;
                default:
                    exp = CrystalDecisions.Shared.ExportFormatType.PortableDocFormat;
                    fileExt = "pdf";
                    mimetype = "application/pdf";
                    break;
            }

            string fileName = Path.Combine(WorkingFolder.GetMajorsilenceTempFolder(), $"{Guid.NewGuid().ToString()}.{fileExt}");
            rpt.ExportToDisk(exp, fileName);

            byte[] myData;
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                myData = new byte[Convert.ToInt32(fs.Length - 1) + 1];
                fs.Read(myData, 0, Convert.ToInt32(fs.Length));
                fs.Close();
            }

            try
            {
                System.IO.File.Delete(fileName);
            }
            catch (Exception)
            {
                // fixme: data cleanup
            }

            return Tuple.Create(myData, fileExt, mimetype);
        }
    }
}