using System.Text;
using System;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Majorsilence.CrystalCmd.Server
{

    public class TokenVerifier
    {
        public static bool VerifyToken(string token, string secretKey)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            // Ensure the token is a valid JWT
            if (!tokenHandler.CanReadToken(token))
            {
                return false;
            }

            // Define the token validation parameters
            var key = Encoding.ASCII.GetBytes(secretKey);
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false, // Validate the issuer
                ValidateAudience = false, // Validate the audience
                ValidateLifetime = true, // Validate the token's lifetime
                ValidateIssuerSigningKey = true, // Validate the signing key
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };

            try
            {
                // Validate the token
                ClaimsPrincipal principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                // Optionally, you can now access claims
                var claims = principal.Claims;

                // Token is valid
                return true;
            }
            catch (Exception ex)
            {
                // Token validation failed
                Console.WriteLine($"Token validation failed: {ex.Message}");
                return false;
            }
        }
    }
}