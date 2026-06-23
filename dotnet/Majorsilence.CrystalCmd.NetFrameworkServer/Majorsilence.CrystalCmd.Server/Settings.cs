using Microsoft.Extensions.Configuration;
using System;

namespace Majorsilence.CrystalCmd.Server
{
    public static class Settings
    {
        public static string GetSetting(string key)
        {
            // Mirror the host's configuration sources (base file, then the environment-specific
            // overlay, then environment variables) so values read here — e.g. Credentials and
            // ExternalLogs — honour appsettings.{Environment}.json exactly like the DI
            // IConfiguration does. Without the overlay, settings placed in
            // appsettings.Development.json are silently ignored by this code path.
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                              ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                              ?? "Production";

            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            return config.GetValue<string>(key);
        }
    }
}
