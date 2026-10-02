using Majorsilence.CrystalCmd.Common;

namespace Majorsilence.CrystalCmd.RptEngine;

/// <summary>
/// The request asks for something this backend cannot render correctly yet: more than one
/// table, data for a subreport, or an export type with no equivalent. Routing (the
/// serviceable rule) is meant to keep such requests on the Crystal Reports worker; one
/// that arrives here anyway fails with the rule it broke, never with a wrong document.
/// </summary>
public sealed class NotServiceableException : CrystalCmdException
{
    public NotServiceableException(string message) : base(message)
    {
    }
}

/// <summary>The queue channels the Majorsilence.Crystal worker consumes.</summary>
public static class RptEngineWorkerChannels
{
    public const string Reports = QueueChannels.RptEngineReports;
    public const string Analyzer = QueueChannels.RptEngineAnalyzer;
}
