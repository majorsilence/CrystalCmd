using System;
using System.Collections.Generic;
using System.Text;

namespace Majorsilence.CrystalCmd.Common
{
    public class GetDataTableColumnsRequest
    {
        public GetDataTableColumnsRequest()
        {

        }

        public GetDataTableColumnsRequest(string tableName, string subreportName = null): this()
        {
            TableName = tableName;
            SubreportName = subreportName;
        }

        public string SubreportName { get; set; }
        public string TableName { get; set; }
    }
}
