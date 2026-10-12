using Microsoft.Data.Sqlite;
using System.Data;

public class SqliteDbProvider(string cnString) : DbProviderBase
{
    private static IDbConnection? _dbConnection;    // Cache the connection to avoid creating multiple connections to the in-memory database

    protected override string? SqlScriptCheckIfDbExists
        => "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Customers')";

    protected override string[] InitializeDbScriptFileNames => ["db-scripts.sqlite.sql"];

    protected override IDbConnection CreateDbConnection()
    {
        if (_dbConnection == null)
        {
            var cn = new SqliteConnection(cnString);
            cn.Open();
            _dbConnection = cn;
        }
        return _dbConnection;
    }

}
