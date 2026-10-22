using Demo.Repositories.FileStorage;
using Demo.Contracts.Repositories;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace Demo.Repositories.DataProviders;

public enum DbServerType
{
    Sqlite,
    SqlServer,
    Postgres
}

public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly DbProviderBase _dbProvider;

    public DbConnectionFactory(IConfiguration configuration)
    {
        if (!Enum.TryParse<DbServerType>(configuration["DbServerType"], ignoreCase: true, out var dbServerType))
            throw new InvalidOperationException("DbServerType must be Sqlite, SqlServer, or Postgres.");

        var connectionString = configuration.GetConnectionString(dbServerType.ToString())
            ?? throw new InvalidOperationException($"Connection string '{dbServerType}' is required.");

        var path = configuration["DbScriptsPath"] ?? throw new InvalidOperationException("DbScriptsPath is required in the configuration.");
        path = new DirectoryInfo(path).FullName;

        string[] getPaths(params string[] paths) =>
           paths.Select(p => Path.Combine(path, p)).ToArray();

        _dbProvider = dbServerType switch
        {
            DbServerType.Sqlite => new SqliteDbProvider(connectionString, getPaths("sale-demo.sqlite.sql", "test-data.sqlite.sql")),
            DbServerType.SqlServer => new SqlServerDbProvider(connectionString, getPaths("sale-demo.sqlserver.sql", "test-data.sqlserver.sql")),
            DbServerType.Postgres => new PostgresDbProvider(connectionString, getPaths("sale-demo.postgres.sql", "test-data.postgres.sql")),
            _ => throw new ArgumentOutOfRangeException(nameof(dbServerType), dbServerType, null)
        };
    }

    public IDbConnection CreateConnection() => _dbProvider.GetDbConnection();
    public string[] GetTableNames() => _dbProvider.GetTableNames();
    public string QuoteIdentifier(string identifier) => _dbProvider.QuoteIdentifier(identifier);
}
