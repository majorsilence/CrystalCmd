import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.Base64;

/**
 * Minimal HS256 JWT validator (no external dependency — uses javax.crypto + gson).
 * Mirrors the C# server's JWT handling: a token is accepted only when signed with the
 * configured key (>= 32 bytes), not expired, and matching the configured issuer/audience
 * when those are set. Returns the subject ("sub") on success, or null on any failure.
 */
public final class JwtValidator {

    // Match .NET's default clock skew tolerance.
    private static final long CLOCK_SKEW_SECONDS = 300;

    private JwtValidator() {
    }

    /** @return the subject claim if the token is valid, otherwise null. */
    public static String validateAndGetSubject(String token) {
        if (!Config.jwtEnabled() || token == null || token.isEmpty()) {
            return null;
        }
        try {
            String[] parts = token.split("\\.");
            if (parts.length != 3) {
                return null;
            }
            String headerB64 = parts[0];
            String payloadB64 = parts[1];
            String signatureB64 = parts[2];

            JsonObject header = parseJson(headerB64);
            JsonElement alg = header.get("alg");
            if (alg == null || !"HS256".equals(alg.getAsString())) {
                return null;
            }

            // Verify signature over "header.payload".
            byte[] key = Config.jwtKey().getBytes(StandardCharsets.UTF_8);
            Mac mac = Mac.getInstance("HmacSHA256");
            mac.init(new SecretKeySpec(key, "HmacSHA256"));
            byte[] expected = mac.doFinal((headerB64 + "." + payloadB64).getBytes(StandardCharsets.US_ASCII));
            byte[] provided = Base64.getUrlDecoder().decode(signatureB64);
            if (!MessageDigest.isEqual(expected, provided)) {
                return null;
            }

            JsonObject payload = parseJson(payloadB64);
            long now = System.currentTimeMillis() / 1000L;

            if (payload.has("exp") && now > payload.get("exp").getAsLong() + CLOCK_SKEW_SECONDS) {
                return null;
            }
            if (payload.has("nbf") && now + CLOCK_SKEW_SECONDS < payload.get("nbf").getAsLong()) {
                return null;
            }

            String expectedIssuer = Config.jwtIssuer();
            if (expectedIssuer != null && !expectedIssuer.isEmpty()) {
                JsonElement iss = payload.get("iss");
                if (iss == null || !expectedIssuer.equals(iss.getAsString())) {
                    return null;
                }
            }

            String expectedAudience = Config.jwtAudience();
            if (expectedAudience != null && !expectedAudience.isEmpty()) {
                if (!audienceMatches(payload.get("aud"), expectedAudience)) {
                    return null;
                }
            }

            JsonElement sub = payload.get("sub");
            return sub != null ? sub.getAsString() : "";
        } catch (Exception e) {
            return null;
        }
    }

    private static boolean audienceMatches(JsonElement aud, String configuredCsv) {
        if (aud == null) {
            return false;
        }
        String[] allowed = configuredCsv.split(",");
        java.util.List<String> tokenAudiences = new java.util.ArrayList<>();
        if (aud.isJsonArray()) {
            for (JsonElement e : aud.getAsJsonArray()) {
                tokenAudiences.add(e.getAsString());
            }
        } else {
            tokenAudiences.add(aud.getAsString());
        }
        for (String a : allowed) {
            String trimmed = a.trim();
            if (trimmed.isEmpty()) {
                continue;
            }
            for (String t : tokenAudiences) {
                if (trimmed.equals(t)) {
                    return true;
                }
            }
        }
        return false;
    }

    private static JsonObject parseJson(String base64Url) {
        byte[] json = Base64.getUrlDecoder().decode(base64Url);
        return JsonParser.parseString(new String(json, StandardCharsets.UTF_8)).getAsJsonObject();
    }
}
