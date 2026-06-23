using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server
{
    internal class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public BasicAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Task.FromResult(AuthenticateResult.NoResult());

            try
            {
                var authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);
                if (!"Basic".Equals(authHeader.Scheme, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(AuthenticateResult.NoResult());

                var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
                var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
                if (credentials.Length != 2)
                    return Task.FromResult(AuthenticateResult.Fail("Invalid Basic authentication header"));

                var username = credentials[0];
                var password = credentials[1];

                var expectedUser = Settings.GetSetting("Credentials:Username");
                var expectedPass = Settings.GetSetting("Credentials:Password");

                if (string.IsNullOrWhiteSpace(expectedUser) || string.IsNullOrWhiteSpace(expectedPass))
                {
                    // Log the real reason server-side, but return a generic failure to the
                    // client so it cannot distinguish "not configured" from "wrong password".
                    Logger.LogError("Basic authentication is not configured (Credentials:Username/Password missing).");
                    return Task.FromResult(AuthenticateResult.Fail("Invalid credentials"));
                }

                // Compare both fields in constant time (case-sensitive). Evaluating both
                // comparisons unconditionally avoids leaking, via response timing, which
                // field was wrong or how many leading characters matched.
                bool userMatches = FixedTimeEquals(username, expectedUser);
                bool passMatches = FixedTimeEquals(password, expectedPass);
                if (!userMatches || !passMatches)
                {
                    return Task.FromResult(AuthenticateResult.Fail("Invalid credentials"));
                }

                var claims = new[] { new Claim(ClaimTypes.Name, username) };
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            catch (FormatException ex)
            {
                Logger.LogError(ex, "Invalid Authorization header format");
                return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization header"));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error authenticating");
                return Task.FromResult(AuthenticateResult.Fail("Error authenticating"));
            }
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            // CryptographicOperations.FixedTimeEquals short-circuits only on length,
            // which is acceptable here; the byte content is compared in constant time.
            var ab = Encoding.UTF8.GetBytes(a ?? string.Empty);
            var bb = Encoding.UTF8.GetBytes(b ?? string.Empty);
            return CryptographicOperations.FixedTimeEquals(ab, bb);
        }
    }
}
