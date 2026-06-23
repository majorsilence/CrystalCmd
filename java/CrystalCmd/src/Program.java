import com.google.gson.Gson;
import com.sun.net.httpserver.HttpContext;
import com.sun.net.httpserver.HttpServer;

import java.io.IOException;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.concurrent.Executors;

/**
 * Entry point. Two modes, mirroring the C# implementation:
 *   - Command line: -reportpath <rpt> -datafile <json> -outpath <out>  (render once to a file)
 *   - Server:       polling HTTP API (/export, /export/poll, /analyzer, /analyzer/poll, /status)
 *                   backed by an in-process work queue + Crystal worker.
 */
public class Program {

    public static void main(String[] args) throws Exception {
        String reportpath = "";
        String dataFilePath = "";
        String outpath = "";

        for (int i = 0; i <= args.length - 1; i++) {
            if (args[i].equals("-reportpath") && i + 1 < args.length) {
                reportpath = args[i + 1].trim();
            } else if (args[i].equals("-datafile") && i + 1 < args.length) {
                dataFilePath = args[i + 1].trim();
            } else if (args[i].equals("-outpath") && i + 1 < args.length) {
                outpath = args[i + 1].trim();
            }
        }

        if (!reportpath.isEmpty() && !dataFilePath.isEmpty() && !outpath.isEmpty()) {
            runCommandLine(reportpath, dataFilePath, outpath);
        } else {
            runServer();
        }
    }

    private static void runCommandLine(String reportpath, String dataFilePath, String outpath) throws Exception {
        String json = new String(Files.readAllBytes(Paths.get(dataFilePath)), StandardCharsets.UTF_8);
        Data data = new Gson().fromJson(json, Data.class);
        Exporter.ExportResult result = new Exporter().export(reportpath, data);
        Files.write(Paths.get(outpath), result.content);
        System.out.println("Wrote " + result.content.length + " bytes (" + result.fileExt + ") to " + outpath);
    }

    private static void runServer() throws IOException {
        // Fail closed on insecure defaults before listening.
        Config.validateSecurity();

        // In-process worker that renders/analyses queued jobs.
        ReportWorker worker = new ReportWorker();
        worker.start();

        int port = Config.port();
        System.out.println("Running in server mode, http://127.0.0.1:" + port + "/status");

        HttpServer server = HttpServer.create(new InetSocketAddress(port), 0);

        AuthFilter auth = new AuthFilter();

        // Health endpoints are unauthenticated.
        server.createContext("/status", new StatusHandler());
        server.createContext("/healthz", new StatusHandler());

        // Protected endpoints (Basic or JWT).
        HttpContext export = server.createContext("/export", new ExportHandler());
        export.getFilters().add(auth);
        HttpContext exportPoll = server.createContext("/export/poll", new ExportHandler());
        exportPoll.getFilters().add(auth);
        HttpContext analyzer = server.createContext("/analyzer", new AnalyzerHandler());
        analyzer.getFilters().add(auth);
        HttpContext analyzerPoll = server.createContext("/analyzer/poll", new AnalyzerHandler());
        analyzerPoll.getFilters().add(auth);

        server.setExecutor(Executors.newFixedThreadPool(8));
        server.start();
    }
}
