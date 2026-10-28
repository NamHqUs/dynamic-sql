using Demo.Repositories.FileStorage;
using Demo.Contracts.Repositories;
using Demo.Contracts.Dto;
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
        if (!Enum.TryParse<DbServerType>(configuration["DbProvider:Default"], ignoreCase: true, out var dbServerType))
            throw new InvalidOperationException("DbProvider:Default must be Sqlite, SqlServer, or Postgres.");

        var connectionString = configuration.GetConnectionString(dbServerType.ToString())
            ?? throw new InvalidOperationException($"Connection string '{dbServerType}' is required.");

        var path = configuration["DbProvider:DbScriptsPath"] ?? throw new InvalidOperationException("DbScriptsPath is required in the configuration.");
        path = new DirectoryInfo(path).FullName;

        var useCases = configuration.GetSection("DbProvider:UseCases").Get<string[]>() ?? Array.Empty<string>();
        var scripts = useCases.Select(s => Path.Combine(path, $"{s}.{dbServerType}.sql".ToLower())).ToArray();

        _dbProvider = dbServerType switch
        {
            DbServerType.Sqlite => new SqliteDbProvider(connectionString, scripts),
            DbServerType.SqlServer => new SqlServerDbProvider(connectionString, scripts),
            DbServerType.Postgres => new PostgresDbProvider(connectionString, scripts),
            _ => throw new ArgumentOutOfRangeException(nameof(dbServerType), dbServerType, null)
        };
    }

    public IDbConnection CreateConnection() => _dbProvider.GetDbConnection();
    public string[] GetTableNames() => _dbProvider.GetTableNames();
    public string QuoteIdentifier(string identifier) => _dbProvider.QuoteIdentifier(identifier);
    public DatabaseSchemaDto GetDatabaseSchema() => _dbProvider.GetDatabaseSchema();
}
