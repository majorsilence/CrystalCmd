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
                    try
                    {
                        SetSubReport(table.ReportName, table.TableName, dt, reportClientDocument);
                    }
                    catch (Exception)
                    {
                        // some sub reports are optional
                        // TODO: logging
                    }
                }



                foreach (var param in datafile.Parameters)
                {
                    SetParameterValue(param.Key, param.Value, reportClientDocument);
                }

                foreach (ParameterField x in reportClientDocument.ParameterFields)
                {
                    if (x.HasCurrentValue == false && x.ReportParameterType == ParameterType.ReportParameter)
                    {
                        // to get things up and running

                        SetParameterValue(x.Name, "", reportClientDocument);
                    }
                    Console.WriteLine(x.Name);
                    Console.WriteLine(x.CurrentValues);
                    Console.WriteLine(x.ParameterValueType);
                }

                return ExportPDF(reportClientDocument);
            }
        }


        private void SetParameterValue(string name, object val, ReportDocument rpt)
        {
            if (rpt.ParameterFields[name] != null)
            {
                var par = rpt.ParameterFields[name];
                string theValue;
                switch (par.ParameterValueType){
                    case ParameterValueKind.BooleanParameter:
                        theValue = string.IsNullOrWhiteSpace(val?.ToString()) ? "false" : val.ToString();
                        rpt.SetParameterValue(name, bool.Parse(theValue));
                        break;
                    case ParameterValueKind.CurrencyParameter:
                        theValue = string.IsNullOrWhiteSpace(val?.ToString()) ? "0" : val.ToString();
                        rpt.SetParameterValue(name, decimal.Parse(theValue));
                        break;
                    case ParameterValueKind.NumberParameter:
                         theValue = string.IsNullOrWhiteSpace(val?.ToString()) ? "0" : val.ToString();
                        try
                        {
                            rpt.SetParameterValue(name, int.Parse(theValue));
                        }
                        catch (Exception)
                        {
                            rpt.SetParameterValue(name, decimal.Parse(theValue));
                        }
                        
                        break;
                    case ParameterValueKind.DateParameter:
                    case ParameterValueKind.DateTimeParameter:
                    case ParameterValueKind.TimeParameter:
                        theValue = string.IsNullOrWhiteSpace(val?.ToString()) ? DateTime.Now.ToLongDateString() : val.ToString();
                        rpt.SetParameterValue(name, DateTime.Parse(theValue));
                        break;
                    default:
                         theValue = string.IsNullOrWhiteSpace(val?.ToString()) ? " " : val.ToString();
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