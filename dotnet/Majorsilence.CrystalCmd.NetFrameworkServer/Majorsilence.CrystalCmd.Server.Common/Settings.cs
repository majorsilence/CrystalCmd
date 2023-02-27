using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server.Common
{
    public static class Settings
    {
        public static string GetSetting(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);

            if (string.IsNullOrWhiteSpace(value))
            {
                value = ConfigurationManager.AppSettings[key];
            }

            return value;
        }
    }
}