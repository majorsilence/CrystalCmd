using Microsoft.Extensions.Configuration;
using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Majorsilence.CrystalCmd.Server
{
    /// <summary>
    /// Binds the polling handle returned by the /export/poll and /analyzer/poll endpoints
    /// to the authenticated caller, so one principal cannot retrieve another principal's
    /// generated report by guessing or replaying its id (an IDOR / missing object-level
    /// authorization issue).
    ///
    /// The handle is "<id>:<base64url(HMACSHA256(key, id|owner))>". On GET, the signature is
    /// recomputed from the current caller's identity and compared in constant time; a
    /// mismatch is treated as "not found".
    ///
    /// Binding is only enforced when a signing key is available (Security:PollTokenKey, or
    /// the JWT signing key). With Basic auth and no key configured there is a single
    /// principal, so cross-user access is not possible and the raw id is returned unchanged
    /// — this keeps existing single-tenant and load-balanced deployments working.
    /// </summary>
    internal static class PollTokenProtector
    {
        private const string PlaceholderJwtKey = "PLACEHOLDER_PLACEHOLDER_PLACEHOLDER_PLACEHOLDER";

        private static bool TryGetKey(IConfiguration configuration, out byte[] key)
        {
            var configured = configuration["Security:PollTokenKey"];
            if (string.IsNullOrWhiteSpace(configured))
            {
                configured = configuration["Jwt:Key"];
            }

            if (string.IsNullOrWhiteSpace(configured)
                || string.Equals(configured, PlaceholderJwtKey, StringComparison.Ordinal))
            {
                key = null;
                return false;
            }

            key = Encoding.UTF8.GetBytes(configured);
            return true;
        }

        public static string OwnerOf(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return "";
            }
            // Prefer a stable subject claim for JWT, fall back to the name (Basic username).
            return user.FindFirst("sub")?.Value
                   ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? user.Identity?.Name
                   ?? "";
        }

        private static string Sign(byte[] key, string id, string owner)
        {
            using var hmac = new HMACSHA256(key);
            var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(id + "|" + owner));
            // base64url without padding so the value is header-safe.
            return Convert.ToBase64String(bytes)
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        /// <summary>Wrap a freshly generated report id into a caller-bound handle.</summary>
        public static string Protect(string id, ClaimsPrincipal user, IConfiguration configuration)
        {
            if (!TryGetKey(configuration, out var key))
            {
                return id;
            }
            return id + ":" + Sign(key, id, OwnerOf(user));
        }

        /// <summary>
        /// Validate a handle presented on GET and recover the underlying report id.
        /// Returns false (caller should answer NotFound) when binding is required but the
        /// signature is missing or does not match the current caller.
        /// </summary>
        public static bool TryUnprotect(string token, ClaimsPrincipal user, IConfiguration configuration, out string id)
        {
            id = token;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (!TryGetKey(configuration, out var key))
            {
                // No key configured: single-principal deployment, accept the raw id.
                return true;
            }

            int idx = token.LastIndexOf(':');
            if (idx <= 0 || idx == token.Length - 1)
            {
                // Binding is required but the handle is unsigned.
                return false;
            }

            var rawId = token.Substring(0, idx);
            var providedSig = token.Substring(idx + 1);
            var expectedSig = Sign(key, rawId, OwnerOf(user));

            var providedBytes = Encoding.UTF8.GetBytes(providedSig);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedSig);
            if (!CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            {
                return false;
            }

            id = rawId;
            return true;
        }
    }
}
