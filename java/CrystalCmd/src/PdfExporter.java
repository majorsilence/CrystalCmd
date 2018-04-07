import com.crystaldecisions.sdk.occa.report.application.OpenReportOptions;
import com.crystaldecisions.sdk.occa.report.application.ReportClientDocument;
import com.crystaldecisions.sdk.occa.report.definition.IReportObject;
import com.crystaldecisions.sdk.occa.report.exportoptions.ReportExportFormat;
import com.crystaldecisions.sdk.occa.report.lib.ReportSDKException;
import com.sun.net.httpserver.HttpServer;

import jj2000.j2k.NotImplementedError;

import com.crystaldecisions.sdk.occa.report.application.ParameterFieldController;

import java.io.ByteArrayInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.net.InetSocketAddress;
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

public class PdfExporter {
	public void exportReport(String reportPath, String outputPath, Data datafile)
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

	private void moveReportObject(ReportClientDocument reportClientDocument, MoveObjects item)
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
