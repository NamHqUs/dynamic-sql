using Npgsql;
using System.Data;

namespace Demo.Repositories.DataProviders;

public class PostgresDbProvider(string cnString, string[] scriptFiles) : DbProviderBase(scriptFiles)
{
    protected override string? SqlScriptCheckIfDbExists =>
        "SELECT CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Customers') THEN 1 ELSE 0 END;";

    protected override IDbConnection CreateDbConnection()
        => new NpgsqlConnection(cnString);
}
