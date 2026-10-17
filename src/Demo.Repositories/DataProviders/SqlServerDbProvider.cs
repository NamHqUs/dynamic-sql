using Microsoft.Data.SqlClient;
using System.Data;

namespace Demo.Repositories.DataProviders;

public class SqlServerDbProvider(string cnString, string[] scriptFiles) : DbProviderBase(scriptFiles)
{
    protected override string? SqlScriptCheckIfDbExists
        => "SELECT IIF(OBJECT_ID('[Customers]', 'U') IS NOT NULL, 1, 0);";

    protected override IDbConnection CreateDbConnection()
        => new SqlConnection(cnString);
}
