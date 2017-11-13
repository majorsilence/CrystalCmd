
import com.crystaldecisions.sdk.occa.report.application.OpenReportOptions;
import com.crystaldecisions.sdk.occa.report.application.ReportClientDocument;
import com.crystaldecisions.sdk.occa.report.definition.IReportObject;
import com.crystaldecisions.sdk.occa.report.exportoptions.ReportExportFormat;
import com.crystaldecisions.sdk.occa.report.lib.ReportSDKException;

import jj2000.j2k.NotImplementedError;

import com.crystaldecisions.sdk.occa.report.application.ParameterFieldController;

import java.io.ByteArrayInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import java.util.Map;
import java.util.Scanner;

public class Program {

	public static void main(String[] args) throws ReportSDKException, IOException, SQLException {
		String reportpath = "";
		String dataFilePath = "";
		Data convertedDataFile = null;
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
			convertedDataFile = gson.fromJson(datafile, Data.class);

			exportReport(reportpath, outpath, convertedDataFile);
		} else {
			System.out.println("Not supported");
			// TODO: load in data from memory stream passed in
			throw new NotImplementedError();
			/*
			 * String data = ReadConsoleInputForData();
			 * 
			 * com.google.gson.Gson gson = new com.google.gson.Gson(); convertedDataFile =
			 * gson.fromJson(data, Data.class);
			 * 
			 * exportReport("/home/peter/Projects/CrystalWrapper/thereport.rpt", outpath,
			 * convertedDataFile);
			 */

		}
	}

	private static String ReadConsoleInputForData() {
		Scanner scanner = new Scanner(System.in); // Use a Scanner.
		List<String> al = new ArrayList<String>(); // The list of names (String(s)).
		String word; // The current line.
		while (scanner.hasNextLine()) { // make sure there is a line.
			word = scanner.nextLine(); // get the line.
			if (word != null) { // make sure it isn't null.
				word = word.trim(); // trim it.
				if (word.equalsIgnoreCase("done")) { // check for done.
					break; // End on "done".
				}
				al.add(word); // Add the line to the list.
			} else {
				break; // End on null.
			}
		}
		scanner.close();
		return al.toString();
	}

	private static String readFile(String path, Charset encoding) throws IOException {
		byte[] encoded = Files.readAllBytes(Paths.get(path));
		return new String(encoded, encoding);
	}

	private static void exportReport(String reportPath, String outputPath, Data datafile)
			throws ReportSDKException, IOException, SQLException {

		ReportClientDocument reportClientDocument;
		ByteArrayInputStream byteArrayInputStream;
		byte[] byteArray;
		int bytesRead;

		/*
		 * Instantiate ReportClientDocument and specify the Java Print Engine as the
		 * report processor. Open a rpt file and export to PDF. Stream PDF back to web
		 * browser.
		 */

		reportClientDocument = new ReportClientDocument();

		reportClientDocument.setReportAppServer(ReportClientDocument.inprocConnectionString);

		reportClientDocument.open(reportPath, OpenReportOptions._openAsReadOnly);

		// Object reportSource = reportClientDocument.getReportSource();

		if (datafile != null) {
			for (Map.Entry<String, Object> item : datafile.getParameters().entrySet()) {
				ParameterFieldController parameterFieldController;

				parameterFieldController = reportClientDocument.getDataDefController().getParameterFieldController();
				parameterFieldController.setCurrentValue("", item.getKey(), item.getValue());
				/*
				 * parameterFieldController.setCurrentValue("", "StringParam", "Hello");
				 * parameterFieldController.setCurrentValue("sub", "StringParam",
				 * "Subreport string value"); parameterFieldController.setCurrentValue("",
				 * "BooleanParam", new Boolean(true));
				 * parameterFieldController.setCurrentValue("", "CurrencyParam", new
				 * Double(123.45)); parameterFieldController.setCurrentValue("", "NumberParam",
				 * new Integer(123));
				 * 
				 */
				// rpt.SetParameterValue(item.Key, item.Value);
			}
			for (Map.Entry<String, String> item : datafile.getDataTables().entrySet()) {

				CsharpResultSet inst = new CsharpResultSet();
				ResultSet result = inst.Execute(item.getValue());

				reportClientDocument.getDatabaseController().setDataSource(result, item.getKey(), item.getKey());

			}
			for (Iterator<MoveObjects> itr = datafile.getMoveObjectPosition().iterator(); itr.hasNext();) {
				MoveObjects item = itr.next();
				moveReportObject(reportClientDocument, item);
			}
		}

		byteArrayInputStream = (ByteArrayInputStream) reportClientDocument.getPrintOutputController()
				.export(ReportExportFormat.PDF);

		byteArray = new byte[1024];
		/*
		 * while((bytesRead = byteArrayInputStream.read(byteArray)) != -1) {
		 * response.getOutputStream().write(byteArray, 0, bytesRead); }
		 */
		FileOutputStream fos = new FileOutputStream(outputPath);
		while ((bytesRead = byteArrayInputStream.read(byteArray)) != -1) {
			fos.write(byteArray, 0, bytesRead);
		}
		fos.close();

		reportClientDocument.close();
	}

	private static void moveReportObject(ReportClientDocument reportClientDocument, MoveObjects item)
			throws ReportSDKException {
		IReportObject control = reportClientDocument.getReportDefController().findObjectByName(item.ObjectName);

		if (item.Pos == MovePosition.LEFT) {
			control.setLeft(item.Move);
		}

		if (item.Type == MoveType.ABSOLUTE) {
			switch (item.Pos) {
			case LEFT:
				control.setLeft(item.Move);
				break;
			case TOP:
				control.setTop(item.Move);
				break;
			}
		} else {
			switch (item.Pos) {
			case LEFT:
				control.setLeft(control.getLeft() + item.Move);
				break;
			case TOP:
				control.setTop(control.getTop() + item.Move);
				break;
			}
		}
	}

}
