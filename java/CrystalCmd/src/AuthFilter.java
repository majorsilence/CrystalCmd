import com.sun.net.httpserver.Filter;
import com.sun.net.httpserver.HttpExchange;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.Base64;

/**
 * Authentication for protected endpoints: HTTP Basic OR JWT bearer, fail-closed (any
 * request without valid credentials gets 401). On success the caller identity is stored as
 * the "owner" exchange attribute so PollTokenProtector can bind poll handles to it.
 *
 * Mirrors the C# server's [Authorize(AuthenticationSchemes = "Bearer,Basic")].
 */
public class AuthFilter extends Filter {

    @Override
    public String description() {
        return "Basic/JWT authentication";
    }

    @Override
    public void doFilter(HttpExchange exchange, Chain chain) throws IOException {
        String owner = authenticate(exchange.getRequestHeaders().getFirst("Authorization"));
        if (owner == null) {
            exchange.getResponseHeaders().add("WWW-Authenticate", "Basic realm=\"CrystalCmd\"");
            HttpUtil.sendText(exchange, 401, "Unauthorized");
            return;
        }
        exchange.setAttribute("owner", owner);
        chain.doFilter(exchange);
    }

    private String authenticate(String header) {
        if (header == null || header.isEmpty()) {
            return null;
        }
        if (header.startsWith("Bearer ")) {
            return JwtValidator.validateAndGetSubject(header.substring("Bearer ".length()).trim());
        }
        if (header.startsWith("Basic ")) {
            String creds;
            try {
                creds = new String(Base64.getDecoder().decode(header.substring("Basic ".length()).trim()),
                        StandardCharsets.UTF_8);
            } catch (IllegalArgumentException e) {
                return null;
            }
            int sep = creds.indexOf(':');
            if (sep < 0) {
                return null;
            }
            String user = creds.substring(0, sep);
            String pass = creds.substring(sep + 1);

            String expUser = Config.username();
            String expPass = Config.password();
            if (expUser == null || expUser.isEmpty() || expPass == null || expPass.isEmpty()) {
                return null; // fail closed when not configured
            }
            // Evaluate both comparisons (non-short-circuit) in constant time.
            boolean ok = fixedEquals(user, expUser) & fixedEquals(pass, expPass);
            return ok ? user : null;
        }
        return null;
    }

    private static boolean fixedEquals(String a, String b) {
        return MessageDigest.isEqual(
                (a == null ? "" : a).getBytes(StandardCharsets.UTF_8),
                (b == null ? "" : b).getBytes(StandardCharsets.UTF_8));
    }
}
