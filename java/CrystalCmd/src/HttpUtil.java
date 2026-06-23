import com.sun.net.httpserver.HttpExchange;

import java.io.IOException;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;

/** Small helpers for writing com.sun.net.httpserver responses. */
public final class HttpUtil {

    private HttpUtil() {
    }

    public static String owner(HttpExchange exchange) {
        Object o = exchange.getAttribute("owner");
        return o == null ? "" : o.toString();
    }

    public static void send(HttpExchange exchange, int status, String contentType, byte[] body) throws IOException {
        if (body == null) {
            body = new byte[0];
        }
        if (contentType != null) {
            exchange.getResponseHeaders().add("Content-Type", contentType);
        }
        exchange.sendResponseHeaders(status, body.length == 0 ? -1 : body.length);
        try (OutputStream os = exchange.getResponseBody()) {
            os.write(body);
        }
    }

    public static void sendText(HttpExchange exchange, int status, String text) throws IOException {
        send(exchange, status, "text/plain; charset=utf-8", text.getBytes(StandardCharsets.UTF_8));
    }

    public static void sendStatus(HttpExchange exchange, int status) throws IOException {
        exchange.sendResponseHeaders(status, -1);
        exchange.close();
    }

    public static void sendFile(HttpExchange exchange, byte[] body, String mimeType, String fileName) throws IOException {
        exchange.getResponseHeaders().add("Content-Disposition", "attachment; filename=\"" + fileName + "\"");
        send(exchange, 200, mimeType, body);
    }
}
