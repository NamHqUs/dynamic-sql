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

    public DbInstance? CurrentDbInstance
    {
        get
        {
            lock (_instanceLock)
                return _currentDbInstance;
        }
    }

    public DbInstance CreateDbInstance(string provider, string connectionString)
    {
        if (!Enum.TryParse(provider, ignoreCase: true, out DbServerType providerType))
            throw new ArgumentException($"Unsupported database '{provider}'.", nameof(provider));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A connection string is required.", nameof(connectionString));

        var scriptFiles = GetInitializationScripts(provider);
        return providerType switch
        {
            DbServerType.Sqlite => new SqliteDbInstance(connectionString, scriptFiles),
            DbServerType.SqlServer => new SqlServerInstance(connectionString, scriptFiles),
            DbServerType.Firebird => new FirebirdDbInstance(connectionString, scriptFiles),
            DbServerType.Postgres => new PostgresDbInstance(connectionString, scriptFiles),
            DbServerType.MySql => new MySqlDbInstance(connectionString, scriptFiles),
            DbServerType.Oracle => new OracleDbInstance(connectionString, scriptFiles),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
    }

    public void SetCurrentDbInstance(DbInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        DbInstance? previous;
        lock (_instanceLock)
        {
            previous = _currentDbInstance;
            _currentDbInstance = instance;
        }

        if (!ReferenceEquals(previous, instance) && previous is IDisposable disposable)
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

    public IDbConnection CreateConnection() => GetCurrentDbInstance().CreateDbConnection();

    public string[] GetTableNames() => GetCurrentDbInstance().GetTableNames();

    public DatabaseSchemaDto GetDatabaseSchema() => GetCurrentDbInstance().GetDatabaseSchema();

    public DatabaseTableDataDto GetTableData(string tableName, string? schema = null, int? limit = null)
        => GetCurrentDbInstance().GetTableData(tableName, schema, limit);

    private DbInstance GetCurrentDbInstance()
    {
        if (CurrentDbInstance is null)
        {
            var optonIndex = configuration.Get<int?>("DbProvider:DefaultDbOptionIndex");
            if (optonIndex.HasValue)
            { 
                var section = configuration.GetSection($"DbOptions:{optonIndex.Value}");
                _currentDbInstance = CreateDbInstance(section["Provider"]!, section["ConnectionString"]!);
            }
        }

        return CurrentDbInstance ?? throw new DatabaseConnectionException();
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
