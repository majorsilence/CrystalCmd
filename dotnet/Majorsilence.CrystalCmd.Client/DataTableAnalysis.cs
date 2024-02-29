using System;
using System.Collections.Generic;
using System.Text;

namespace Majorsilence.CrystalCmd.Client
{
    public class DataTableAnalysis
    {
        internal DataTableAnalysis(string dataTableName, IEnumerable<string> columnNames)
        {
            DataTableName = dataTableName;
            ColumnNames = columnNames;
        }

        public string DataTableName { get; }
        public IEnumerable<string> ColumnNames { get; }

        public override string ToString()
        {
            var builder = new StringBuilder();
            var seperatorCount = 15;
            var seperator = new string('-', seperatorCount);
            builder.AppendLine("Data Table: " + DataTableName);
            builder.AppendLine(seperator);
            builder.AppendLine("Columns: " + string.Join(", ", ColumnNames));
            builder.AppendLine(seperator);
            return builder.ToString();
        }
    }
}
