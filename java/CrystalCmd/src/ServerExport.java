import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.nio.file.StandardOpenOption;
import java.sql.SQLException;
import java.util.HashSet;
import org.apache.commons.fileupload.FileItemIterator;
import org.apache.commons.fileupload.FileItemStream;
import org.apache.commons.fileupload.FileUpload;
import org.apache.commons.io.IOUtils;

import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;
import com.crystaldecisions.sdk.occa.report.lib.ReportSDKException;

public class ServerExport implements HttpHandler {

	HashSet<FileData> files;

	public void handle(HttpExchange t) throws IOException {
		OutputStream os = null;
		try {
			FileItemIterator ii = new FileUpload().getItemIterator(new ExchangeRequestContext(t));
			os = t.getResponseBody();
			files = new HashSet<FileData>();
			while (ii.hasNext()) {
				final FileItemStream is = ii.next();
				final String name = is.getFieldName();

				try (InputStream stream = is.openStream()) {
					final String filename = is.getName();
					FileData file22 = new FileData();
					file22.data = IOUtils.toByteArray(stream);
					file22.fileName = filename;
					file22.name = name;
					files.add(file22);
				}
			}

			if (files.isEmpty() == true) {
				os.close();
				return;
			}

			FileData reportTemplate;
			FileData reportData;
			if (((FileData) files.toArray()[0]).name.equals("reportdata")) {
				reportData = (FileData) files.toArray()[0];
				reportTemplate = (FileData) files.toArray()[1];
			} else {
				reportData = (FileData) files.toArray()[1];
				reportTemplate = (FileData) files.toArray()[0];
			}

			// Start regular report code
			File pathReportTemplate = saveReportTemplate(reportTemplate.data);

			com.google.gson.Gson gson = new com.google.gson.Gson();

			String json = new String(reportData.data);
			Data convertedDataFile = gson.fromJson(json, Data.class);

			ByteArrayInputStream report;

			//

			String templateFilePath = pathReportTemplate.getAbsolutePath();
			PdfExporter pdfExport = new PdfExporter();
			report = pdfExport.exportReportToStream(templateFilePath, convertedDataFile);

			Files.delete(Paths.get(templateFilePath));

			t.sendResponseHeaders(200, report.available());
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
		} catch (Exception e) {
			e.printStackTrace();
		}

		os.close();
	}

	private File saveReportTemplate(byte[] reportTemplate) throws IOException {
		File temp = File.createTempFile("temp-crystalcmd-template-", ".tmp");
		// byte[] b = reportTemplate.getBytes(StandardCharsets.UTF_8);
		Files.write(Paths.get(temp.getAbsolutePath()), reportTemplate, StandardOpenOption.WRITE);
		return temp;
	}

	public static class FileData {
		public String name;
		public String fileName;
		public String contentType;
		public byte[] data;
	}

}
