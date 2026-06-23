/**
 * Work-queue database dialects. H2 is the default because its JDBC driver ships in lib/;
 * the others mirror the C# server but require their JDBC driver jar to be added to lib/
 * (and the MANIFEST Class-Path) before use.
 */
public enum SqlType {
    H2,
    SQLITE,
    POSTGRESQL,
    SQLSERVER;

    public static SqlType parse(String sqlType) {
        if (sqlType == null) {
            return H2;
        }
        switch (sqlType.trim().toLowerCase()) {
            case "sqlite":
                return SQLITE;
            case "postgre":
            case "postgres":
            case "postgresql":
            case "psql":
                return POSTGRESQL;
            case "mssql":
            case "sql":
            case "sqlserver":
                return SQLSERVER;
            case "h2":
            default:
                return H2;
        }
    }
}
