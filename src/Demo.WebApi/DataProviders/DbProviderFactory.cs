using System.Data;

public enum DbServerType
{
    Sqlite,
    SqlServer,
    Postgres
}

public static class DbProviderFactory
{
    private static DbProviderBase? _dbProvider;

    public static void Initialize(IConfiguration configuration)
    {
        var dbServerType = configuration.Get<DbServerType>("DbServerType");
        var cnString = configuration.GetConnectionString(dbServerType.ToString())!;
        _dbProvider = dbServerType switch
        {
            DbServerType.Sqlite => new SqliteDbProvider(cnString),
            DbServerType.SqlServer => new SqlServerDbProvider(cnString),
            DbServerType.Postgres => new PostgresDbProvider(cnString),
            _ => throw new ArgumentOutOfRangeException(nameof(dbServerType), dbServerType, null)
        };
    }

    public static IDbConnection GetDbConnection()
        => _dbProvider?.GetDbConnection() ?? throw new InvalidOperationException("DbProviderFactory is not initialized. Call Initialize() first.");
   

    public static IDbCommand CreateDbCommand(string commandText, CommandType commandType = CommandType.Text)
    {
        var cn = GetDbConnection();
        if (cn.State != ConnectionState.Open)
            cn.Open();
        var command = cn.CreateCommand();

        command.CommandText = commandText;
        command.CommandType = commandType;
        return command;
    }
}
