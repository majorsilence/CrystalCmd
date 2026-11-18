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
    /// Response headers on <c>POST /export</c> and <c>POST /export/poll</c> that name the backend the
    /// server routed the request to, and why, so a caller can show which engine rendered a report.
    /// </summary>
    public static class RenderBackendHeaders
    {
        /// <summary>The <see cref="RenderBackend"/> name: <c>Crystal</c> or <c>RptEngine</c>, never <c>Auto</c>.</summary>
        public const string Backend = "X-CrystalCmd-Backend";

        /// <summary>The routing decision's reason, for example "Crystal, because the report has a cross-tab".</summary>
        public const string Reason = "X-CrystalCmd-Backend-Reason";
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
