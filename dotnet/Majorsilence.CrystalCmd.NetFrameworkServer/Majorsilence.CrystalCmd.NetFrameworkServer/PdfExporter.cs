using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;

namespace Majorsilence.CrystalCmd.NetFrameworkServer
{
    public class PdfExporter
    {

        public byte[] exportReportToStream(string reportPath, Client.Data datafile)
        {
            using (var reportClientDocument = new ReportDocument())
            {
                //reportClientDocument.ReportAppServer = "inproc:jrc";
                reportClientDocument.Load(reportPath);



            


                foreach (var table in datafile.DataTables)
                {
                    DataTable dt = CreateTable(table.Value);
                    SetDataSource(table.Key, dt, reportClientDocument);
                }

                foreach (var table in datafile.SubReportDataTables)
                {
                    // fixme: sub report with multiple datatables?
                    DataTable dt = CreateTable(table.DataTable);
                    SetSubReport(table.ReportName, table.TableName, dt, reportClientDocument);
                }



                foreach (var param in datafile.Parameters)
                {
                    SetParameterValue(param.Key, param.Value, reportClientDocument);
                }

                //foreach (ParameterField x in reportClientDocument.ParameterFields)
                //{
                //    if (x.HasCurrentValue == false)
                //    {
                //        // why is this needed here but not in financial and web portals?
                //    }
                //    Console.WriteLine(x.Name);
                //    Console.WriteLine(x.CurrentValues);
                //    Console.WriteLine(x.ParameterValueType);
                //}

                return ExportPDF(reportClientDocument);
            }
        }


        private void SetParameterValue(string name, object val, ReportDocument rpt)
        {
            if (rpt.ParameterFields[name] != null)
            {
                var par = rpt.ParameterFields[name];
                switch (par.ParameterValueType){
                    case ParameterValueKind.BooleanParameter:
                        rpt.SetParameterValue(name, bool.Parse(val.ToString()));
                        break;
                    case ParameterValueKind.CurrencyParameter:
                        rpt.SetParameterValue(name, decimal.Parse( val.ToString()));
                        break;
                    case ParameterValueKind.NumberParameter:
                        rpt.SetParameterValue(name, int.Parse(val.ToString()));
                        break;
                    case ParameterValueKind.DateParameter:
                    case ParameterValueKind.DateTimeParameter:
                    case ParameterValueKind.TimeParameter:
                        rpt.SetParameterValue(name, DateTime.Parse(val.ToString()));
                        break;
                    default:
                        var theValue = string.IsNullOrWhiteSpace(val?.ToString()) ? " " : val.ToString();
                        rpt.SetParameterValue(name, theValue);
                        break;
                }
               
            }
            else
            {
                Console.WriteLine(name);
            }
        }

        private void SetDataSource(string tableName, DataTable val, ReportDocument rpt)
        {
            rpt.Database.Tables[tableName].SetDataSource(val);
        }

        private void SetSubReport(string rptName, string reportTableName, DataTable dataSource, ReportDocument rpt)
        {
            rpt.Subreports[rptName].Database.Tables[reportTableName].SetDataSource(dataSource);
        }


        private byte[] ExportPDF(ReportDocument rpt)
        {

            string fileName = System.IO.Path.GetTempFileName();
            CrystalDecisions.Shared.ExportFormatType exp = ExportFormatType.PortableDocFormat;
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

            return myData;
        }

        private DataTable CreateTable(string csv)
        {
            DataTable dt = new DataTable();
            using (StringReader sr = new StringReader(csv))
            {
                // (?<!\\),   - , seperated csv and works with \, escapted commas
                string[] headers = Regex.Split(sr.ReadLine(), @"(?<!\\),");

                string[] columntypes = Regex.Split(sr.ReadLine(), @"(?<!\\),");
                for(int i = 0; i< headers.Length; i++)
                {
                    dt.Columns.Add(headers[i], Type.GetType($"System.{columntypes[i]}",false, true));
                }

                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] columns = Regex.Split(line, @"(?<!\\),");
                    DataRow dr = dt.NewRow();
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cleaned = columns[i].Substring(1, columns[i].Length - 2).Replace(@"\,", ",");
                        if (string.IsNullOrWhiteSpace(cleaned))
                        {
                            dr[i] = DBNull.Value;
                        }
                        else {
                            dr[i] = cleaned;
                        }
                    }
                    dt.Rows.Add(dr);
                }

            }


            return dt;
        }

    }



}