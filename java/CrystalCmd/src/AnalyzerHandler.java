import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;

/**
 * Handles /analyzer (synchronous analysis) and /analyzer/poll (POST enqueue, GET fetch).
 * Mirrors the C# AnalyzerController. Only the report template is required (no report data).
 */
public class AnalyzerHandler implements HttpHandler {

    private static final String CHANNEL = "crystal-analyzer";

    @Override
    public void handle(HttpExchange exchange) {
        String method = exchange.getRequestMethod();
        String path = exchange.getRequestURI().getPath();
        try {
            if (path.equals("/analyzer") && method.equalsIgnoreCase("POST")) {
                handleSynchronous(exchange);
            } else if (path.equals("/analyzer/poll") && method.equalsIgnoreCase("POST")) {
                handlePollPost(exchange);
            } else if (path.equals("/analyzer/poll") && method.equalsIgnoreCase("GET")) {
                handlePollGet(exchange);
            } else {
                HttpUtil.sendStatus(exchange, 404);
            }
        } catch (RequestReader.BadRequestException bre) {
            trySend(exchange, bre.status, bre.getMessage());
        } catch (Exception e) {
            e.printStackTrace();
            trySend(exchange, 500, "");
        }
    }

    private void handleSynchronous(HttpExchange exchange) throws Exception {
        RequestReader.ReadResult input = RequestReader.read(exchange, true);
        WorkQueue queue = WorkQueue.createDefault(CHANNEL);
        queue.enqueue(QueueItem.create(input.id, input.template, null));

        for (int i = 0; i < 60; i++) {
            WorkQueue.GetResult result = queue.get(input.id);
            if (result.status == WorkItemStatus.Processing || result.status == WorkItemStatus.Pending) {
                Thread.sleep(500);
                continue;
            }
            if (result.status == WorkItemStatus.Completed && result.report != null) {
                HttpUtil.send(exchange, 200, "application/json", result.report.FileContent);
                return;
            }
            HttpUtil.sendStatus(exchange, 500);
            return;
        }
        HttpUtil.sendStatus(exchange, 500);
    }

    private void handlePollPost(HttpExchange exchange) throws Exception {
        RequestReader.ReadResult input = RequestReader.read(exchange, true);
        WorkQueue queue = WorkQueue.createDefault(CHANNEL);
        queue.enqueue(QueueItem.create(input.id, input.template, null));
        String handle = PollTokenProtector.protect(input.id, HttpUtil.owner(exchange));
        HttpUtil.sendText(exchange, 200, handle);
    }

    private void handlePollGet(HttpExchange exchange) throws Exception {
        String token = exchange.getRequestHeaders().getFirst("id");
        if (token == null || token.trim().isEmpty()) {
            HttpUtil.sendStatus(exchange, 400);
            return;
        }
        String id = PollTokenProtector.tryUnprotect(token, HttpUtil.owner(exchange));
        if (id == null) {
            HttpUtil.sendStatus(exchange, 404);
            return;
        }
        WorkQueue queue = WorkQueue.createDefault(CHANNEL);
        WorkQueue.GetResult result = queue.get(id);
        switch (result.status) {
            case Unknown:
                HttpUtil.sendStatus(exchange, 404);
                break;
            case Completed:
                HttpUtil.send(exchange, 200, "application/json", result.report.FileContent);
                break;
            case Failed:
                HttpUtil.sendStatus(exchange, 500);
                break;
            case Processing:
            case Pending:
                HttpUtil.sendText(exchange, 202, "Processing report");
                break;
            default:
                HttpUtil.sendText(exchange, 452, "Unknown");
                break;
        }
    }

    private static void trySend(HttpExchange exchange, int status, String message) {
        try {
            if (message == null || message.isEmpty()) {
                HttpUtil.sendStatus(exchange, status);
            } else {
                HttpUtil.sendText(exchange, status, message);
            }
        } catch (Exception ignored) {
        }
    }
}
