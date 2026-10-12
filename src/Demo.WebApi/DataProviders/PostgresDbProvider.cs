using Npgsql;
using System.Data;

public class PostgresDbProvider(string cnString) : DbProviderBase
{
    protected override string? SqlScriptCheckIfDbExists =>
        "SELECT CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Customers') THEN 1 ELSE 0 END;";

    protected override string[] InitializeDbScriptFileNames => ["db-scripts.postgres.sql"];

    protected override IDbConnection CreateDbConnection()
        => new NpgsqlConnection(cnString);
}
