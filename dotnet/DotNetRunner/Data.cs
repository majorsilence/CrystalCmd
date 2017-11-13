using System;
using System.Collections.Generic;

namespace DotNetRunner
{
    public class Data
    {
        public byte[] ReportFile { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public IEnumerable<MoveObjects> MoveObjectPosition { get; set; }

        // DataTables converted to xml, must be loaded into new DataTables
        public Dictionary<string, string> DataTables { get; set; }
    }

    public class MoveObjects
    {
        public string ObjectName { get; set; }
        public int Move { get; set; }
        public MoveType Type { get; set; }
        public MovePosition Pos { get; set; }
    }

    public enum MoveType
    {
        ABSOLUTE = 1,
        RELATIVE = 2
    }

    public enum MovePosition
    {
        TOP = 1,
        LEFT = 2
    }

    public enum ReportType
    {
        /// <summary>
        /// The report type is PDF.  You can save the report as a pdf file that can then be
        /// viewed in any standard PDF viewer.
        /// </summary>
        PDF = 1,

        /// <summary>
        /// Only used for item that will be exported as zip.  Generally reports will not be in a zip file.
        /// </summary>
        ZIP = 3,

        /// <summary>
        /// Do not generate a report.
        /// </summary>
        NONE = 4
    }
}
