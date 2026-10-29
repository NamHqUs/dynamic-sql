using Demo.DbServices.DbInstances;
using Demo.Contracts.Dto;
using Demo.Contracts.Exceptions;
using Demo.Contracts.Services;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace Demo.DbServices;

public class DatabaseService(IConfiguration configuration) : IDatabaseService, IDisposable
{
    private readonly object _instanceLock = new();
    private DbInstance? _currentDbInstance;

    public DbInstance CurrentDbInstance
    {
        get
        {
            if (_currentDbInstance is null)
            {
                var optonIndex = configuration.Get<int?>("DbProvider:DefaultDbOptionIndex");
                if (optonIndex.HasValue)
                {
                    var section = configuration.GetSection($"DbOptions:{optonIndex.Value}");
                    _currentDbInstance = CreateDbInstance(section["Provider"]!, section["ConnectionString"]!);
                }
            }

            return _currentDbInstance ?? throw new DatabaseConnectionException();
        }
    }

    public void Connect(string provider, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(connectionString);
        DbInstance? previous;
        lock (_instanceLock)
        {
            previous = _currentDbInstance;
            _currentDbInstance = CreateDbInstance(provider, connectionString);
        }

        if (!ReferenceEquals(previous, _currentDbInstance) && previous is IDisposable disposable)
            disposable.Dispose();
    }

    public void Disconnect()
    {
        // The database instance is shared by all clients, so a client disconnect must not end its lifetime.
    }

    public void Dispose()
    {
        DbInstance? previous;
        lock (_instanceLock)
        {
            previous = _currentDbInstance;
            _currentDbInstance = null;
        }

        if (previous is IDisposable disposable)
            disposable.Dispose();
    }

    public IDbConnection CreateConnection() => CurrentDbInstance.CreateDbConnection();

    public string[] GetTableNames() => CurrentDbInstance.GetTableNames();

    public DatabaseSchemaDto GetDatabaseSchema() => CurrentDbInstance.GetDatabaseSchema();

    public DatabaseTableDataDto GetTableData(string tableName, string? schema = null, int? limit = null)
        => CurrentDbInstance.GetTableData(tableName, schema, limit);

    private DbInstance CreateDbInstance(string provider, string connectionString)
    {
        if (!Enum.TryParse(provider, ignoreCase: true, out DbServerType providerType))
            throw new ArgumentException($"Unsupported database '{provider}'.", nameof(provider));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A connection string is required.", nameof(connectionString));

        var scriptFiles = GetInitializationScripts(provider);
        return providerType switch
        {
            DbServerType.Sqlite => new SqliteDbInstance(connectionString, scriptFiles),
            DbServerType.SqlServer => new SqlServerDbInstance(connectionString, scriptFiles),
            DbServerType.Firebird => new FirebirdDbInstance(connectionString, scriptFiles),
            DbServerType.Postgres => new PostgresDbInstance(connectionString, scriptFiles),
            DbServerType.MySql => new MySqlDbInstance(connectionString, scriptFiles),
            DbServerType.Oracle => new OracleDbInstance(connectionString, scriptFiles),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
    }

    private string[] GetInitializationScripts(string provider)
    {
        var path = configuration["DbProvider:DbScriptsPath"]
            ?? throw new InvalidOperationException("DbProvider:DbScriptsPath is required in the configuration.");
        path = new DirectoryInfo(path).FullName;

        var useCases = configuration.GetSection("DbProvider:UseCases").Get<string[]>() ?? [];
        return useCases
            .Select(useCase => Path.Combine(path, $"{useCase}.{provider}.sql".ToLowerInvariant()))
            .Where(fileName => File.Exists(Path.Combine(Environment.CurrentDirectory, fileName)))
            .ToArray();
    }
}
