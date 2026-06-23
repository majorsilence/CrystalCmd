import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;

import com.sun.net.httpserver.BasicAuthenticator;

/**
 * HTTP Basic authentication for the Java server. Expected credentials are read from the
 * CRYSTALCMD_USERNAME and CRYSTALCMD_PASSWORD environment variables. When either is
 * unset the authenticator denies all requests (fail closed) so the /export endpoint is
 * never exposed without credentials.
 */
public class BasicAuth extends BasicAuthenticator {

    private final String expectedUser;
    private final String expectedPass;

    public BasicAuth(String realm) {
        super(realm);
        this.expectedUser = System.getenv("CRYSTALCMD_USERNAME");
        this.expectedPass = System.getenv("CRYSTALCMD_PASSWORD");
        if (this.expectedUser == null || this.expectedUser.isEmpty()
                || this.expectedPass == null || this.expectedPass.isEmpty()) {
            System.err.println(
                "WARNING: CRYSTALCMD_USERNAME / CRYSTALCMD_PASSWORD are not set; "
                + "all /export requests will be rejected.");
        }
    }

    @Override
    public boolean checkCredentials(String username, String password) {
        if (expectedUser == null || expectedUser.isEmpty()
                || expectedPass == null || expectedPass.isEmpty()) {
            return false;
        }
        // Constant-time comparison of both fields to avoid leaking via timing which field
        // (or how many characters) matched.
        boolean userOk = constantTimeEquals(username, expectedUser);
        boolean passOk = constantTimeEquals(password, expectedPass);
        return userOk && passOk;
    }

    private static boolean constantTimeEquals(String a, String b) {
        byte[] ab = (a == null ? "" : a).getBytes(StandardCharsets.UTF_8);
        byte[] bb = (b == null ? "" : b).getBytes(StandardCharsets.UTF_8);
        return MessageDigest.isEqual(ab, bb);
    }
}
