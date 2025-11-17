using Microsoft.Extensions.Configuration;

namespace Majorsilence.CrystalCmd.Server
{
    public static class Settings
    {
        public static string GetSetting(string key)
        {
            IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            return config.GetValue<string>(key);
        }
    }
}
