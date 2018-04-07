
import com.crystaldecisions.sdk.occa.report.lib.ReportSDKException;
import com.sun.net.httpserver.HttpServer;

import java.io.IOException;
import java.net.InetSocketAddress;
import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.sql.SQLException;

public class Program {

	public static void main(String[] args) throws ReportSDKException, IOException, SQLException {
		String reportpath = "";
		String dataFilePath = "";
		
		String outpath = "";

		for (int i = 0; i <= args.length - 1; i++) {
			System.out.println(args[i]);
			if (args[i].equals("-reportpath")) {
				reportpath = args[i + 1].trim();
			} else if (args[i].equals("-datafile")) {
				dataFilePath = args[i + 1].trim();
			} else if (args[i].equals("-outpath")) {
				outpath = args[i + 1].trim();
			}
		}

		System.out.println("-args length: " + args.length);
		System.out.println("-reportpath: " + reportpath);
		System.out.println("-datafile: " + dataFilePath);
		System.out.println("-outpath: " + outpath);

		if (reportpath.trim().isEmpty() == false && dataFilePath.trim().isEmpty() == false) {
			/// Load from report and data from file system

			String datafile = readFile(dataFilePath, StandardCharsets.UTF_8);
			com.google.gson.Gson gson = new com.google.gson.Gson();
			Data convertedDataFile = gson.fromJson(datafile, Data.class);

			PdfExporter pdfExport = new PdfExporter();
			pdfExport.exportReportToFile(reportpath, outpath, convertedDataFile);
		} else {
			System.out.println("Running in server mode");
			HttpServer server = HttpServer.create(new InetSocketAddress(4321), 0);
			server.createContext("/status", new ServerStatus());
			server.createContext("/export", new ServerExport());
			server.setExecutor(null); // creates a default executor
			server.start();

		}
	}

	private static String readFile(String path, Charset encoding) throws IOException {
		byte[] encoded = Files.readAllBytes(Paths.get(path));
		return new String(encoded, encoding);
	}

	
}
