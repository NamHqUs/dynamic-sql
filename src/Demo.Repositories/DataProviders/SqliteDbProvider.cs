using Microsoft.Data.Sqlite;
using System.Data;

namespace Demo.Repositories.DataProviders;

public class SqliteDbProvider : DbProviderBase
{
    private readonly string _connectionString;
    private readonly bool _isInMemory;
    private readonly object _keepAliveLock = new();
    private SqliteConnection? _keepAliveConnection;

    public SqliteDbProvider(string cnString, string[] scriptFiles) : base(scriptFiles)
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder(cnString);
        _isInMemory = connectionStringBuilder.DataSource == ":memory:";
        if (_isInMemory)
        {
            connectionStringBuilder.DataSource = $"DynamicSqlDemo_{Guid.NewGuid():N}";
            connectionStringBuilder.Mode = SqliteOpenMode.Memory;
            connectionStringBuilder.Cache = SqliteCacheMode.Shared;
        }

        _connectionString = connectionStringBuilder.ToString();
    }

    protected override string? SqlScriptCheckIfDbExists
        => "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Customers')";

    protected override IDbConnection CreateDbConnection()
    {
        if (_isInMemory)
        {
            lock (_keepAliveLock)
            {
                if (_keepAliveConnection is null)
                {
                    _keepAliveConnection = new SqliteConnection(_connectionString);
                    _keepAliveConnection.Open();
                }
            }
        }

        return new SqliteConnection(_connectionString);
    }

}
