import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.Base64;

/**
 * Java port of the C# PollTokenProtector. Binds the polling handle returned by
 * /export/poll and /analyzer/poll to the authenticated caller so one principal cannot
 * retrieve another's report by guessing/replaying its id.
 *
 * Handle format: "<id>:<base64url(HMACSHA256(key, id|owner))>". When no signing key is
 * configured (CRYSTALCMD_POLL_TOKEN_KEY / CRYSTALCMD_JWT_KEY) the raw id is returned and
 * accepted unchanged, preserving single-principal/backward-compatible behaviour.
 */
public final class PollTokenProtector {

    private PollTokenProtector() {
    }

    private static byte[] key() {
        String k = Config.pollTokenKey();
        if (k == null || k.isEmpty()) {
            return null;
        }
        return k.getBytes(StandardCharsets.UTF_8);
    }

    private static String sign(byte[] key, String id, String owner) {
        try {
            Mac mac = Mac.getInstance("HmacSHA256");
            mac.init(new SecretKeySpec(key, "HmacSHA256"));
            byte[] sig = mac.doFinal((id + "|" + (owner == null ? "" : owner)).getBytes(StandardCharsets.UTF_8));
            return Base64.getUrlEncoder().withoutPadding().encodeToString(sig);
        } catch (Exception e) {
            throw new RuntimeException("Failed to compute poll token signature", e);
        }
    }

    /** Wrap a freshly generated report id into a caller-bound handle. */
    public static String protect(String id, String owner) {
        byte[] key = key();
        if (key == null) {
            return id;
        }
        return id + ":" + sign(key, id, owner);
    }

    /**
     * Validate a handle and recover the underlying report id. Returns null when binding is
     * required but the handle is missing/invalid for this caller (caller should answer 404).
     */
    public static String tryUnprotect(String token, String owner) {
        if (token == null || token.isEmpty()) {
            return null;
        }
        byte[] key = key();
        if (key == null) {
            // No key configured: single-principal deployment, accept the raw id.
            return token;
        }
        int idx = token.lastIndexOf(':');
        if (idx <= 0 || idx == token.length() - 1) {
            return null; // binding required but handle is unsigned
        }
        String rawId = token.substring(0, idx);
        String providedSig = token.substring(idx + 1);
        String expectedSig = sign(key, rawId, owner);
        byte[] a = providedSig.getBytes(StandardCharsets.UTF_8);
        byte[] b = expectedSig.getBytes(StandardCharsets.UTF_8);
        if (!MessageDigest.isEqual(a, b)) {
            return null;
        }
        return rawId;
    }
}
