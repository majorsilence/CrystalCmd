import java.io.*;
//import java.sql.Connection;
//import java.sql.DriverManager;
import java.nio.file.FileSystems;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.sql.*;
import java.time.format.DateTimeFormatter;
import java.util.Properties;
//import java.sql.ResultSetMetaData;

import org.h2.store.fs.FileUtils;
import org.h2.tools.Csv;
import org.h2.tools.SimpleResultSet;


public class CsharpResultSet {

    Connection conn;
    Statement stmt;

    public ResultSet Execute(String csv) throws SQLException, IOException {
        // Java NIO

        // HACK: since csvjdbc seems to require a folder path instead of csv name
        // stick each report into their own unique directory to avoid
        // cross pollination
        String tmpdir = System.getProperty("java.io.tmpdir");
        String subfolder = java.util.UUID.randomUUID().toString();
        String finalTempDir = tmpdir +
                //FileSystems.getDefault().getSeparator() +
                subfolder;
        Path path = Paths.get(finalTempDir);
        Files.createDirectories(path);

        String tableAndFileName = java.util.UUID.randomUUID().toString();
        String csvAbsolutePath = path.toString() +
                FileSystems.getDefault().getSeparator() +
                tableAndFileName + ".csv";

        System.out.println("Temp file : " + csvAbsolutePath);

        BufferedWriter writer = new BufferedWriter(new FileWriter(csvAbsolutePath));
        writer.write(csv);

        writer.close();

        String[] lines = csv.split("\\n");
        String headers = lines[0];
        String dataTypes = lines[1]
                .replace("Decimal", "Double")
                .replace("Int32", "int")
                .replace("Int64", "long")
                .replace("DateTime", "DATE")
                .replace("Byte[]", "String");

        // https://github.com/simoc/csvjdbc/blob/master/docs/doc.md
        Properties props = new Properties();
        // Define column names and column data types here.
        props.put("suppressHeaders", "true");
        props.put("headerline", headers);
        props.put("columnTypes", dataTypes);
        props.put("skipLeadingLines", 2);
        // "06/24/2020 00:00:00"
        // TODO: How to handle multiple formats
        //props.put("timestampFormat", "");
        props.put("useDateTimeFormatter", "true");

        /*
        DateTimeFormatter formatter = DateTimeFormatter.ofPattern(""
                + "[MM/dd/yyyy HH:mm:ss]"
                + "[yyyy-MM-dd'T'HH:mm:ss]"
                + "[yyyy/MM/dd HH:mm:ss.SSSSSS]"
                + "[yyyy-MM-dd HH:mm:ss[.SSS]]"
                + "[ddMMMyyyy:HH:mm:ss.SSS[ Z]]"
        );

         */

        //props.put("columnTypes", "Int,Double,Date");
        // try (Connection conn = DriverManager.getConnection("jdbc:relique:csv:" + finalTempDir, props);

        conn = DriverManager.getConnection("jdbc:relique:csv:" + finalTempDir, props);
        // create a scrollable Statement so we can move forwards and backwards
        // through ResultSets
        stmt = conn.createStatement(ResultSet.TYPE_SCROLL_SENSITIVE,
                ResultSet.CONCUR_READ_ONLY);
        ResultSet results = stmt.executeQuery("SELECT * FROM \"" + tableAndFileName + "\"");
        return results;



        /*
        Reader reader = new StringReader(csv);
        ResultSet rs = new CsvReader().read(reader);
        return rs;

         */
    }


    public void close() throws SQLException {
        stmt.close();
        conn.close();
    }

}