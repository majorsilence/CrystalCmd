import com.google.gson.Gson;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;

/**
 * Background worker, the Java counterpart of the C# Console ExportQueue. Polls the work
 * queue on the report and analyzer channels, renders/analyses each claimed item via the
 * Crystal engine, and stores the result back in the queue for the polling endpoints.
 */
public class ReportWorker {

    private static final Gson GSON = new Gson();

    private volatile boolean running = true;
    private final List<Thread> threads = new ArrayList<>();

    public void start() {
        startChannel("crystal-reports");
        startChannel("crystal-analyzer");
    }

    public void stop() {
        running = false;
    }

    private void startChannel(String channel) {
        Thread t = new Thread(() -> runLoop(channel), "crystalcmd-worker-" + channel);
        t.setDaemon(true);
        threads.add(t);
        t.start();
    }

    private void runLoop(String channel) {
        WorkQueue queue = WorkQueue.createDefault(channel);
        try {
            queue.migrate();
        } catch (Exception e) {
            System.out.println("Worker (" + channel + ") migrate failed: " + e.getMessage());
        }

        while (running) {
            final boolean[] processed = {false};
            try {
                queue.dequeue(item -> {
                    processed[0] = true;
                    return process(item);
                });
            } catch (Exception e) {
                System.out.println("Worker (" + channel + ") error: " + e.getMessage());
            }
            if (!processed[0]) {
                try {
                    Thread.sleep(1000);
                } catch (InterruptedException ie) {
                    Thread.currentThread().interrupt();
                    return;
                }
            }
        }
    }

    private GeneratedReport process(QueueItem item) throws Exception {
        File dir = new File(System.getProperty("java.io.tmpdir")
                + File.separator + "crystalcmd" + File.separator + item.Id);
        dir.mkdirs();
        File rpt = new File(dir, item.Id + ".rpt");
        Files.write(rpt.toPath(), item.templateBytes());

        try {
            GeneratedReport report = new GeneratedReport();
            report.Id = item.Id;
            report.GeneratedUtc = Instant.now();

            if (item.Data != null) {
                Exporter.ExportResult res = new Exporter().export(rpt.getAbsolutePath(), item.Data);
                report.FileContent = res.content;
                report.Format = res.fileExt;
                report.Metadata = res.mimeType;
                report.FileName = item.Id + "." + res.fileExt;
            } else {
                FullReportAnalysisResponse analysis = new CrystalReportsAnalyzer().getFullAnalysis(rpt.getAbsolutePath());
                report.FileContent = GSON.toJson(analysis).getBytes(StandardCharsets.UTF_8);
                report.Format = "json";
                report.Metadata = "application/json";
                report.FileName = item.Id + "_analysis.json";
            }
            return report;
        } finally {
            try {
                deleteRecursive(dir);
            } catch (Exception ignored) {
            }
        }
    }

    private static void deleteRecursive(File f) {
        if (f.isDirectory()) {
            File[] children = f.listFiles();
            if (children != null) {
                for (File c : children) {
                    deleteRecursive(c);
                }
            }
        }
        f.delete();
    }
}
