import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;

import java.io.IOException;

/** Health/status endpoints (/status, /healthz, /healthz/ready, /healthz/live). Unauthenticated. */
public class StatusHandler implements HttpHandler {

    @Override
    public void handle(HttpExchange exchange) throws IOException {
        HttpUtil.sendText(exchange, 200, "I'm alive");
    }
}
