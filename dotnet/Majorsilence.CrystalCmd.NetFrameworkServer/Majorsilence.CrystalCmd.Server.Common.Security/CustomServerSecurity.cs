using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server.Common
{
    public static class CustomServerSecurity
    {
        public static (string UserName, string Password)? GetUserNameAndPassword(NameValueCollection headers)
        {
            var authHeader = headers.GetValues("Authorization")?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Basic"))
            {
                return null;
            }
            var auth = authHeader.Replace("Basic ", "");
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(auth ?? ""));

            if (string.IsNullOrWhiteSpace(credentials))
            {
                return null;
            }

            int separator = credentials.IndexOf(':');
            string name = credentials.Substring(0, separator);
            string password = credentials.Substring(separator + 1);

            return (name, password);
        }


        public static string GetBearerToken(NameValueCollection headers)
        {
            var authHeader = headers.GetValues("Authorization")?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer"))
            {
                return null;
            }

            var token = authHeader.Replace("Bearer ", "");
            return token;
        }
    }
}
