using Demo.Contracts.Exceptions;
using Demo.Contracts.Services;
using Demo.DbServices.DbInstances;
using Microsoft.Extensions.Configuration;
using System.Data.Common;
using System.Data;

namespace Demo.DbServices;

public enum DbServerType
{
    Sqlite,
    SqlServer,
    Firebird,
    Postgres,
    MySql,
    Oracle
}

public sealed class DbConnectionService(IConfiguration configuration, DatabaseService dbInstanceFactory) : IDbConnectionService
{
    private readonly IReadOnlyDictionary<string, (string Provider, string ConnectionString)> _dbOptions =
        configuration.GetSection("DbOptions")
            .GetChildren()
            .ToDictionary(e => e["Name"]!, e => (e["Provider"]!, e["ConnectionString"]!)) ?? [];

    private readonly bool _directConnectionsEnabled = configuration.GetValue<bool>("DbProvider:AllowDirectConnections");

    DatabaseConnectionOptionsDto IDbConnectionService.GetConnectionOptions()
        => new(
            Enum.GetNames<DbServerType>(),
            _dbOptions.Keys.ToList(),

            _directConnectionsEnabled);

    string IDbConnectionService.Connect(string profile)
    {
        var (provider, connectionString) = _dbOptions.TryGetValue(profile, out var option)
            ? option
            : throw new KeyNotFoundException($"Database options profile '{profile}' was not found.");

        Connect(provider, connectionString, isDirectConnection: false);
        return provider;
    }
    void IDbConnectionService.Connect(string provider, string connectionString)
        => Connect(provider, connectionString, isDirectConnection: true);

    private void Connect(string provider, string connectionString, bool isDirectConnection)
    {
        if (isDirectConnection && !_directConnectionsEnabled)
            throw new InvalidOperationException("Direct database connections are disabled by the server.");

        DbInstance? instance = null;

        try
        {
            instance = dbInstanceFactory.CreateDbInstance(provider, connectionString);
            using var connection = instance.CreateDbConnection();
            connection.Open();
            dbInstanceFactory.SetCurrentDbInstance(instance);
        }
        catch (DbException exception)
        {
            if (instance is IDisposable disposable)
                disposable.Dispose();
            throw new DatabaseConnectionException(exception);
        }
    }

    void IDbConnectionService.Disconnect() => dbInstanceFactory.Disconnect();

    IDbConnection IDbConnectionService.CreateConnection()
        => dbInstanceFactory.CreateConnection();
}
