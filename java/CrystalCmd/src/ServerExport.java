import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.nio.file.StandardOpenOption;
import java.sql.SQLException;
import java.util.HashMap;
import java.util.Map;

import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;
import com.crystaldecisions.sdk.occa.report.lib.ReportSDKException;

public class ServerExport implements HttpHandler {
	public void handle(HttpExchange t) throws IOException {

		Map<String, String> parameters = getParameters(t);
		PdfExporter pdfExport = new PdfExporter();

		String reportTemplate = parameters.get("reporttemplate");
		String reportData = parameters.get("reportdata");

		File pathReportTemplate = saveReportTemplate(reportTemplate);

		com.google.gson.Gson gson = new com.google.gson.Gson();
		Data convertedDataFile = gson.fromJson(reportData, Data.class);

		ByteArrayInputStream report;
		OutputStream os = t.getResponseBody();
		// t.sendResponseHeaders(200, response.length());
		try {
			report = pdfExport.exportReportToStream(pathReportTemplate.getAbsolutePath(), convertedDataFile);

			Files.delete(Paths.get(pathReportTemplate.getAbsolutePath()));

			byte[] byteArray;
			int bytesRead;
			byteArray = new byte[1024];
			while ((bytesRead = report.read(byteArray)) != -1) {
				os.write(byteArray, 0, bytesRead);
			}
		} catch (ReportSDKException e) {
			// TODO Auto-generated catch block
			e.printStackTrace();
		} catch (SQLException e) {
			// TODO Auto-generated catch block
			e.printStackTrace();
		}

		os.close();
	}

	private File saveReportTemplate(String reportTemplate) throws IOException {
		File temp = File.createTempFile("temp-crystalcmd-template-", ".tmp");
		byte[] b = reportTemplate.getBytes(StandardCharsets.UTF_8);
		Files.write(Paths.get(temp.getAbsolutePath()), b, StandardOpenOption.WRITE);
		return temp;
	}

	Map<String, String> getParameters(HttpExchange httpExchange) throws IOException {
		Map<String, String> parameters = new HashMap<>();
		InputStream inputStream = httpExchange.getRequestBody();
		ByteArrayOutputStream byteArrayOutputStream = new ByteArrayOutputStream();
		byte[] buffer = new byte[2048];
		int read = 0;
		while ((read = inputStream.read(buffer)) != -1) {
			byteArrayOutputStream.write(buffer, 0, read);
		}
		String[] keyValuePairs = byteArrayOutputStream.toString().split("&");
		for (String keyValuePair : keyValuePairs) {
			String[] keyValue = keyValuePair.split("=");
			if (keyValue.length != 2) {
				continue;
			}
			parameters.put(keyValue[0], keyValue[1]);
		}
		return parameters;
	}

}
