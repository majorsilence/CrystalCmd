using Majorsilence.CrystalCmd.Common;
using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.Extensions.Logging;
using System;

namespace Majorsilence.CrystalCmd.Server.Common
{
    /// <summary>
    /// The shared <see cref="BackendHealthCheck"/> on the Crystal Reports runtime, which
    /// is the backend when none is given.
    /// </summary>
    public class HealthCheckTask : BackendHealthCheck
    {
        /// <param name="exporter">The backend to prove alive; the Crystal runtime when null.</param>
        public HealthCheckTask(ILogger logger, string rptFilePath, bool failureShouldExitProcess,
            TimeSpan? checkInterval = null, IReportExporter exporter = null)
            : base(logger, exporter ?? new Exporter(logger), rptFilePath, failureShouldExitProcess, checkInterval)
        {
        }
    }
}
