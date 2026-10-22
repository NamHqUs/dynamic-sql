using Microsoft.Data.SqlClient;
using System.Data;

namespace Demo.Repositories.DataProviders;

public class SqlServerDbProvider(string cnString, string[] scriptFiles) : DbProviderBase(scriptFiles)
{
    protected override string? SqlScriptCheckIfDbExists
        => "SELECT IIF(OBJECT_ID('[Customers]', 'U') IS NOT NULL, 1, 0);";

    protected override string SqlGetTableNames
        => "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;";

    protected override string IdentifierQuotePrefix => "[";
    protected override string IdentifierQuoteSuffix => "]";

    protected override IDbConnection CreateDbConnection()
        => new SqlConnection(cnString);
}
