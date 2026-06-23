/**
 * Central configuration for the Java server, read from environment variables (mirrors the
 * settings the C# server reads from appsettings.json / environment variables).
 *
 *   CRYSTALCMD_USERNAME / CRYSTALCMD_PASSWORD   - Basic auth credentials
 *   CRYSTALCMD_JWT_KEY / _ISSUER / _AUDIENCE    - JWT (HS256) validation (>= 32 bytes to enable)
 *   CRYSTALCMD_POLL_TOKEN_KEY                   - HMAC key binding poll handles (defaults to JWT key)
 *   CRYSTALCMD_MAX_REQUEST_BODY_BYTES           - request body cap (default 100 MB)
 *   CRYSTALCMD_MAX_DECOMPRESSED_BYTES           - gzip decompression cap (default 200 MB)
 *   CRYSTALCMD_WORKQUEUE_SQLTYPE                - h2 | sqlite | postgresql | sqlserver (default h2)
 *   CRYSTALCMD_WORKQUEUE_CONNECTION             - JDBC URL for the work queue
 *   CRYSTALCMD_ALLOW_DEFAULT_CREDENTIALS        - "true" permits user/password (local testing only)
 *   CRYSTALCMD_PORT                             - listen port (default 4321)
 */
public final class Config {

    public static final String DEFAULT_USERNAME = "user";
    public static final String DEFAULT_PASSWORD = "password";
    public static final String PLACEHOLDER_JWT_KEY = "PLACEHOLDER_PLACEHOLDER_PLACEHOLDER_PLACEHOLDER";

    public static final long DEFAULT_MAX_REQUEST_BODY_BYTES = 104_857_600L;   // 100 MB
    public static final long DEFAULT_MAX_DECOMPRESSED_BYTES = 209_715_200L;   // 200 MB

    private Config() {
    }

    public static String get(String name) {
        String v = System.getenv(name);
        return (v == null || v.isEmpty()) ? null : v;
    }

    public static String get(String name, String defaultValue) {
        String v = get(name);
        return v == null ? defaultValue : v;
    }

    public static boolean isTrue(String name) {
        String v = get(name);
        return v != null && v.equalsIgnoreCase("true");
    }

    public static long getLong(String name, long defaultValue) {
        String v = get(name);
        if (v == null) {
            return defaultValue;
        }
        try {
            return Long.parseLong(v.trim());
        } catch (NumberFormatException e) {
            return defaultValue;
        }
    }

    public static String username() {
        return get("CRYSTALCMD_USERNAME");
    }

    public static String password() {
        return get("CRYSTALCMD_PASSWORD");
    }

    public static String jwtKey() {
        String k = get("CRYSTALCMD_JWT_KEY");
        if (k == null || k.equals(PLACEHOLDER_JWT_KEY)) {
            return null;
        }
        return k;
    }

    /** JWT is honoured only with a real key of at least 32 bytes (256-bit HS256). */
    public static boolean jwtEnabled() {
        String k = jwtKey();
        return k != null && k.getBytes(java.nio.charset.StandardCharsets.UTF_8).length >= 32;
    }

    public static String jwtIssuer() {
        return get("CRYSTALCMD_JWT_ISSUER");
    }

    public static String jwtAudience() {
        return get("CRYSTALCMD_JWT_AUDIENCE");
    }

    /** Key used to bind poll handles to the caller; falls back to the JWT key. */
    public static String pollTokenKey() {
        String k = get("CRYSTALCMD_POLL_TOKEN_KEY");
        if (k != null) {
            return k;
        }
        return jwtKey();
    }

    public static long maxRequestBodyBytes() {
        return getLong("CRYSTALCMD_MAX_REQUEST_BODY_BYTES", DEFAULT_MAX_REQUEST_BODY_BYTES);
    }

    public static long maxDecompressedBytes() {
        return getLong("CRYSTALCMD_MAX_DECOMPRESSED_BYTES", DEFAULT_MAX_DECOMPRESSED_BYTES);
    }

    public static String workQueueSqlType() {
        return get("CRYSTALCMD_WORKQUEUE_SQLTYPE", "h2");
    }

    public static String workQueueConnection() {
        // Default to an embedded H2 file database (the H2 driver ships in lib/).
        return get("CRYSTALCMD_WORKQUEUE_CONNECTION",
                "jdbc:h2:file:" + System.getProperty("java.io.tmpdir") + "/crystalcmd-workqueue;AUTO_SERVER=TRUE");
    }

    public static int port() {
        return (int) getLong("CRYSTALCMD_PORT", 4321);
    }

    /**
     * Fail closed on insecure defaults. Throws when the well-known user/password are in use
     * unless CRYSTALCMD_ALLOW_DEFAULT_CREDENTIALS=true.
     */
    public static void validateSecurity() {
        if (isTrue("CRYSTALCMD_ALLOW_DEFAULT_CREDENTIALS")) {
            System.err.println("WARNING: CRYSTALCMD_ALLOW_DEFAULT_CREDENTIALS is enabled. Local testing only.");
            return;
        }
        if (DEFAULT_USERNAME.equals(username()) && DEFAULT_PASSWORD.equals(password())) {
            throw new IllegalStateException(
                "Refusing to start with the default Basic credentials (user/password). "
                + "Set CRYSTALCMD_USERNAME and CRYSTALCMD_PASSWORD, or set "
                + "CRYSTALCMD_ALLOW_DEFAULT_CREDENTIALS=true for local testing only.");
        }
    }
}
