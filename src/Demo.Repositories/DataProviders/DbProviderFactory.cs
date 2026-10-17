using System.Data;
using Demo.ServiceContracts.Repositories;
using Microsoft.Extensions.Configuration;

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

        _dbProvider = dbServerType switch
        {
            DbServerType.Sqlite => new SqliteDbProvider(connectionString),
            DbServerType.SqlServer => new SqlServerDbProvider(connectionString),
            DbServerType.Postgres => new PostgresDbProvider(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(dbServerType), dbServerType, null)
        };
    }

    public IDbConnection CreateConnection() => _dbProvider.GetDbConnection();
}
