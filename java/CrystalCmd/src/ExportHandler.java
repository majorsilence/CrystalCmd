import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;

import java.io.IOException;

/**
 * Handles /export (synchronous render) and /export/poll (POST enqueue, GET fetch). Mirrors
 * the C# ExportController. Poll handles are bound to the caller via PollTokenProtector.
 */
public class ExportHandler implements HttpHandler {

    private static final String CHANNEL = "crystal-reports";

    @Override
    public void handle(HttpExchange exchange) throws IOException {
        String method = exchange.getRequestMethod();
        String path = exchange.getRequestURI().getPath();
        try {
            if (path.equals("/export") && method.equalsIgnoreCase("POST")) {
                handleSynchronous(exchange);
            } else if (path.equals("/export/poll") && method.equalsIgnoreCase("POST")) {
                handlePollPost(exchange);
            } else if (path.equals("/export/poll") && method.equalsIgnoreCase("GET")) {
                handlePollGet(exchange);
            } else {
                HttpUtil.sendStatus(exchange, 404);
            }
        } catch (RequestReader.BadRequestException bre) {
            HttpUtil.sendText(exchange, bre.status, bre.getMessage());
        } catch (Exception e) {
            e.printStackTrace();
            HttpUtil.sendStatus(exchange, 500);
        }
    }

    private void handleSynchronous(HttpExchange exchange) throws Exception {
        RequestReader.ReadResult input = RequestReader.read(exchange, false);
        WorkQueue queue = WorkQueue.createDefault(CHANNEL);
        queue.enqueue(QueueItem.create(input.id, input.template, input.reportData));

        for (int i = 0; i < 60; i++) {
            WorkQueue.GetResult result = queue.get(input.id);
            if (result.status == WorkItemStatus.Processing || result.status == WorkItemStatus.Pending) {
                Thread.sleep(500);
                continue;
            }
            if (result.status == WorkItemStatus.Completed && result.report != null) {
                String mime = "pdf".equals(result.report.Format) ? "application/pdf" : "application/octet-stream";
                HttpUtil.sendFile(exchange, result.report.FileContent, mime, "report." + result.report.Format);
                return;
            }
            HttpUtil.sendText(exchange, 500, "Report generation failed. Use the export/poll flow for long reports.");
            return;
        }
        HttpUtil.sendText(exchange, 500, "Timed out waiting. Use the export/poll POST then GET flow.");
    }

    private void handlePollPost(HttpExchange exchange) throws Exception {
        RequestReader.ReadResult input = RequestReader.read(exchange, false);
        WorkQueue queue = WorkQueue.createDefault(CHANNEL);
        queue.enqueue(QueueItem.create(input.id, input.template, input.reportData));
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
                String mime = "pdf".equals(result.report.Format) ? "application/pdf" : "application/octet-stream";
                HttpUtil.sendFile(exchange, result.report.FileContent, mime, result.report.FileName);
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
}
