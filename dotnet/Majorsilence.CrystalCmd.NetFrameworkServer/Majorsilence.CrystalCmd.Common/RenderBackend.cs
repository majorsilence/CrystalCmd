namespace Majorsilence.CrystalCmd.Common
{
    /// <summary>
    /// Which rendering backend a request asks for. Unset on a request means the server's
    /// configured default (<c>Routing:DefaultBackend</c>, <see cref="Crystal"/> unless set).
    /// </summary>
    public enum RenderBackend
    {
        /// <summary>The SAP Crystal Reports runtime: every report, every export type.</summary>
        Crystal = 1,

        /// <summary>
        /// The Majorsilence.Crystal engine: no SAP runtime, but only reports the serviceable
        /// rule accepts (one table, no subreport with its own data, no cross-tab or chart, an
        /// export type it has). A request that asks for it and fails the rule is refused with
        /// the rule it failed.
        /// </summary>
        RptEngine = 2,

        /// <summary>
        /// Let the server decide: the template is parsed when the request arrives and goes
        /// to <see cref="RptEngine"/> if the serviceable rule accepts it, else to
        /// <see cref="Crystal"/>.
        /// </summary>
        Auto = 3
    }

    /// <summary>
    /// The work-queue channels the server enqueues on and each worker consumes. The Crystal
    /// worker listens on the first pair, the Majorsilence.Crystal worker on the second.
    /// </summary>
    public static class QueueChannels
    {
        public const string CrystalReports = "crystal-reports";
        public const string CrystalAnalyzer = "crystal-analyzer";
        public const string RptEngineReports = "rptengine-reports";
        public const string RptEngineAnalyzer = "rptengine-analyzer";
    }
}
