using System;

namespace Majorsilence.CrystalCmd.Client
{
    [Obsolete("This enum is obsolete, use Majorsilence.CrystalCmd.Common.ExportTypes instead.")]
    public enum ExportTypes
    {
        CSV,
        CrystalReport,
        Excel,
        ExcelDataOnly,
        PDF,
        RichText,
        TEXT,
        WordDoc
    }
}
