using System;
using System.Collections.Generic;
using System.IO;

namespace Majorsilence.CrystalCmd.Client
{
    public class Data
    {
        public Data()
        {
            Parameters = new Dictionary<string, object>();
            MoveObjectPosition = new List<MoveObjects>();
            DataTables = new Dictionary<string, string>();
            SubReportDataTables = new List<SubReports>();
        }

        // public byte[] ReportFile { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public IEnumerable<MoveObjects> MoveObjectPosition { get; set; }

        // DataTables converted to CSV, must be loaded into new DataTables
        public Dictionary<string, string> DataTables { get; set; }
        public List<SubReports> SubReportDataTables { get; set; }
    }
}
