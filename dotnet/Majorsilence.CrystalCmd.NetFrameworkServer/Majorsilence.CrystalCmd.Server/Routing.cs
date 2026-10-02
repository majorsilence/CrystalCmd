using Majorsilence.CrystalCmd.Common;
using Majorsilence.CrystalCmd.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Majorsilence.CrystalCmd.Server
{
    /// <summary>
    /// The controllers' one call into the router: reads the server default from
    /// configuration, decides, and logs the decision with the request id so a report that
    /// went to an unexpected worker can be traced. A refusal (<see cref="BackendRefusedException"/>)
    /// is left to the controller, which answers 400 with the rule the request failed.
    /// </summary>
    internal static class Routing
    {
        public static RoutingDecision Route(Data data, byte[] template, string id, IConfiguration configuration, ILogger logger)
        {
            var defaultBackend = BackendRouter.ParseDefault(configuration[BackendRouter.DefaultBackendSetting]);
            try
            {
                var decision = BackendRouter.Route(data, template, defaultBackend);
                logger.LogInformation("Request {Id} routed to {Backend}: {Reason}", id, decision.Backend, decision.Reason);
                return decision;
            }
            catch (BackendRefusedException ex)
            {
                logger.LogWarning("Request {Id} refused: {Message}", id, ex.Message);
                throw;
            }
        }
    }
}
