namespace Majorsilence.CrystalCmd.Client
{
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
