using Majorsilence.CrystalCmd.Common;
using Majorsilence.CrystalCmd.Server.Common;
using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Majorsilence.CrystalCmd.NetframeworkConsole
{
    /// <summary>
    /// The queue loop (<see cref="QueueProcessor"/>) on the Crystal Reports runtime, with
    /// the channel names the server enqueues on for it.
    /// </summary>
    internal class ExportQueue : QueueProcessor
    {
        // Channel names must match what the server controllers enqueue on
        // (ExportController/AnalyzerController in Majorsilence.CrystalCmd.Server).
        public const string ReportsChannel = "crystal-reports";
        public const string AnalyzerChannel = "crystal-analyzer";

        /// <summary>A queue worker on the Crystal Reports runtime.</summary>
        public ExportQueue(ILogger logger, string channel)
            : this(logger, channel, new Exporter(logger), new CrystalReportsAnalyzer())
        {
        }

        /// <summary>A queue worker on whichever backend implements the two interfaces.</summary>
        public ExportQueue(ILogger logger, string channel, IReportExporter exporter, IReportAnalyzer analyzer)
            : base(logger, channel, exporter, analyzer,
                () => WorkQueue.CreateDefault(channel), WorkingFolder.GetMajorsilenceTempFolder())
        {
        }

        public static List<ExportQueue> Create(ILogger logger, string channel, int threadCount = 1)
        {
            var queues = new List<ExportQueue>();
            for (int i = 0; i < threadCount; i++)
            {
                queues.Add(new ExportQueue(logger, channel));
            }

            return queues;
        }
    }
}
