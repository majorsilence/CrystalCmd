using System;

namespace Majorsilence.CrystalCmd.Common
{
    /// <summary>
    /// Renders a report template with the data, parameters and overrides in a request.
    /// Implemented by the Crystal Reports runtime today; the seam a second backend plugs
    /// into. Lives in this shared project because a .NET Framework worker and a .NET
    /// worker both have to see it.
    /// </summary>
    public interface IReportExporter
    {
        /// <param name="reportPath">Path to the .rpt template on disk.</param>
        /// <param name="data">The request: tables, parameters, overrides and the export type.</param>
        ReportExport Export(string reportPath, Data data);
    }

    /// <summary>The rendered output: its bytes, file extension (no dot) and media type.</summary>
    public sealed class ReportExport
    {
        public ReportExport(byte[] content, string extension, string mediaType)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Extension = extension ?? throw new ArgumentNullException(nameof(extension));
            MediaType = mediaType ?? throw new ArgumentNullException(nameof(mediaType));
        }

        public byte[] Content { get; }
        public string Extension { get; }
        public string MediaType { get; }
    }
}
