using Demo.ServiceContracts.Repositories;
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

        var path = configuration["DbScriptsFilePath"] ?? throw new InvalidOperationException("DbScriptsFilePath is required.");
        path = Path.Combine(Environment.CurrentDirectory, path);

        _dbProvider = dbServerType switch
        {
            DbServerType.Sqlite => new SqliteDbProvider(connectionString, [Path.Combine(path, "db-scripts.sqlite.sql")]),
            DbServerType.SqlServer => new SqlServerDbProvider(connectionString, [Path.Combine(path, "db-scripts.sqlserver.sql")]),
            DbServerType.Postgres => new PostgresDbProvider(connectionString, [Path.Combine(path, "db-scripts.postgres.sql")]),
            _ => throw new ArgumentOutOfRangeException(nameof(dbServerType), dbServerType, null)
        };
    }

    public IDbConnection CreateConnection() => _dbProvider.GetDbConnection();
}
