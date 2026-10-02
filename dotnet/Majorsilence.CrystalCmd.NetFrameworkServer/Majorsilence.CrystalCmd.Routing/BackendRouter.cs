using Majorsilence.CrystalCmd.Common;

namespace Majorsilence.CrystalCmd.Routing;

/// <summary>Where a request goes, and why, when the why is worth logging.</summary>
public sealed class RoutingDecision
{
    public RoutingDecision(RenderBackend backend, string reason)
    {
        Backend = backend;
        Reason = reason;
    }

    public RenderBackend Backend { get; }

    /// <summary>How the decision was reached: the request's own choice, the default, or the rule and its reasons.</summary>
    public string Reason { get; }

    public string ReportsChannel => Backend == RenderBackend.RptEngine ? QueueChannels.RptEngineReports : QueueChannels.CrystalReports;
    public string AnalyzerChannel => Backend == RenderBackend.RptEngine ? QueueChannels.RptEngineAnalyzer : QueueChannels.CrystalAnalyzer;
}

/// <summary>
/// A request asked for a backend that cannot serve it. The caller chose; the server answers
/// with the rule the request failed rather than rendering it wrong or somewhere else.
/// </summary>
public sealed class BackendRefusedException : CrystalCmdException
{
    public BackendRefusedException(RenderBackend backend, Serviceability serviceability)
        : base($"The {backend} backend cannot render this request: {serviceability}.")
    {
        Backend = backend;
        Serviceability = serviceability;
    }

    public RenderBackend Backend { get; }
    public Serviceability Serviceability { get; }
}

/// <summary>
/// Chooses the backend for a request. Three inputs, in priority order: the request's own
/// <see cref="Data.Backend"/>; the server's configured default; and for <see cref="RenderBackend.Auto"/>
/// the serviceable rule, applied to the request and a parse of the template.
/// </summary>
public static class BackendRouter
{
    public const string DefaultBackendSetting = "Routing:DefaultBackend";

    /// <param name="data">The request; null for an analysis, which has only a template.</param>
    /// <param name="defaultBackend">The server's default, used when the request names none.</param>
    public static RoutingDecision Route(Data? data, byte[] template, RenderBackend defaultBackend)
    {
        var requested = data?.Backend;
        var backend = requested ?? defaultBackend;
        string origin = requested is null ? "the server default" : "the request";

        switch (backend)
        {
            case RenderBackend.Crystal:
                return new RoutingDecision(RenderBackend.Crystal, $"Crystal, chosen by {origin}");

            case RenderBackend.RptEngine:
            {
                var serviceability = ServiceableRule.Evaluate(data, template);
                if (!serviceability.Serviceable)
                    throw new BackendRefusedException(RenderBackend.RptEngine, serviceability);
                return new RoutingDecision(RenderBackend.RptEngine, $"RptEngine, chosen by {origin}");
            }

            case RenderBackend.Auto:
            {
                var serviceability = ServiceableRule.Evaluate(data, template);
                return serviceability.Serviceable
                    ? new RoutingDecision(RenderBackend.RptEngine, "RptEngine, by the serviceable rule")
                    : new RoutingDecision(RenderBackend.Crystal, $"Crystal, because {serviceability}");
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(defaultBackend), backend, "Unknown render backend.");
        }
    }

    /// <summary>
    /// Reads the server default from its setting: Crystal, RptEngine or Auto, case-insensitive;
    /// Crystal when unset or unrecognised, so a deployment that never heard of this behaves as before.
    /// </summary>
    public static RenderBackend ParseDefault(string? setting)
    {
        return Enum.TryParse<RenderBackend>(setting, ignoreCase: true, out var parsed)
               && Enum.IsDefined(parsed)
            ? parsed
            : RenderBackend.Crystal;
    }
}
