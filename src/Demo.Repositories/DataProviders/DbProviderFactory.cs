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
    const string FOLDER_NAME = "DbScripts";
    private readonly DbProviderBase _dbProvider;

    public DbConnectionFactory(IConfiguration configuration, FilePathResolver filePathResolver)
    {
        if (!Enum.TryParse<DbServerType>(configuration["DbServerType"], ignoreCase: true, out var dbServerType))
            throw new InvalidOperationException("DbServerType must be Sqlite, SqlServer, or Postgres.");

        var connectionString = configuration.GetConnectionString(dbServerType.ToString())
            ?? throw new InvalidOperationException($"Connection string '{dbServerType}' is required.");

        var path = filePathResolver.GetBuiltinStoreFolder(FOLDER_NAME);

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
