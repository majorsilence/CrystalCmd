using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Majorsilence.CrystalCmd.Server
{
    internal class StatusRoute : BaseRoute
    {
        private static string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        public StatusRoute(ILogger logger) : base(logger)
        {
        }

        // Kept for compatibility; use controllers instead in ASP.NET Core
        public string GetStatus()
        {
            return $"I'm alive {version}";
        }
    }
}
