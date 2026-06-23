import com.google.gson.Gson;
import com.sun.net.httpserver.HttpExchange;
import org.apache.commons.fileupload.FileItemIterator;
import org.apache.commons.fileupload.FileItemStream;
import org.apache.commons.fileupload.FileUpload;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.UUID;
import java.util.zip.GZIPInputStream;

/**
 * Parses an /export or /analyzer request body into report data + template bytes. Mirrors the
 * C# BaseRoute.ReadInput: supports both a gzip-compressed StreamedRequest JSON body and a
 * multipart/form-data body (reportdata JSON part + report template part). Enforces a request
 * body cap and a decompression cap (zip-bomb guard).
 */
public class RequestReader {

    private static final Gson GSON = new Gson();

    public static final class ReadResult {
        public Data reportData;
        public byte[] template;
        public String id;
    }

    public static final class BadRequestException extends Exception {
        public final int status;

        public BadRequestException(int status, String message) {
            super(message);
            this.status = status;
        }
    }

    public static ReadResult read(HttpExchange exchange, boolean templateOnly) throws Exception {
        long max = Config.maxRequestBodyBytes();
        String lenHeader = exchange.getRequestHeaders().getFirst("Content-Length");
        if (lenHeader != null) {
            try {
                if (Long.parseLong(lenHeader.trim()) > max) {
                    throw new BadRequestException(413, "Request body exceeds the maximum allowed size.");
                }
            } catch (NumberFormatException ignored) {
            }
        }

        String contentType = exchange.getRequestHeaders().getFirst("Content-Type");
        String contentEncoding = exchange.getRequestHeaders().getFirst("Content-Encoding");
        if (contentType == null || contentType.isEmpty()) {
            throw new BadRequestException(400, "content type is null");
        }

        boolean gzip = (contentEncoding != null && contentEncoding.equalsIgnoreCase("gzip"))
                || contentType.toLowerCase().contains("gzip");

        ReadResult result = new ReadResult();
        result.id = UUID.randomUUID().toString();

        if (gzip) {
            try (GZIPInputStream gz = new GZIPInputStream(exchange.getRequestBody())) {
                byte[] bytes = readLimited(gz, Config.maxDecompressedBytes());
                StreamedRequest req = GSON.fromJson(new String(bytes, StandardCharsets.UTF_8), StreamedRequest.class);
                if (req == null) {
                    throw new BadRequestException(400, "request body is null");
                }
                result.reportData = req.getReportData();
                result.template = req.getTemplateBytes();
            }
        } else {
            FileItemIterator it = new FileUpload().getItemIterator(new ExchangeRequestContext(exchange));
            while (it.hasNext()) {
                FileItemStream item = it.next();
                String name = item.getFieldName();
                try (InputStream s = item.openStream()) {
                    byte[] bytes = readLimited(s, max);
                    if (name != null && name.equalsIgnoreCase("reportdata")) {
                        result.reportData = GSON.fromJson(new String(bytes, StandardCharsets.UTF_8), Data.class);
                    } else {
                        result.template = bytes;
                    }
                }
            }
        }

        if (!templateOnly && result.reportData == null) {
            throw new BadRequestException(400, "report data is null");
        }
        if (result.template == null || result.template.length == 0) {
            throw new BadRequestException(400, "report template is null");
        }
        return result;
    }

    private static byte[] readLimited(InputStream in, long max) throws IOException {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        byte[] buf = new byte[8192];
        long total = 0;
        int n;
        while ((n = in.read(buf)) != -1) {
            total += n;
            if (total > max) {
                throw new IOException("Request body exceeded the maximum allowed size of " + max + " bytes.");
            }
            out.write(buf, 0, n);
        }
        return out.toByteArray();
    }
}
