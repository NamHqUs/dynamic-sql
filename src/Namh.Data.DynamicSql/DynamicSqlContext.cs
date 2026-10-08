using Namh.Data.DynamicSql.Internal.Query;
using System.Data;

namespace Namh.Data.DynamicSql;

public class DynamicSqlContext(
        IDbConnection dbConnection,
        IMetaProvider metaProvider,
        ISqlDialect? sqlDialect = null) : IDisposable
{
    private readonly ISqlDialect _sqlDialect =  sqlDialect ?? SqlDialect.ForConnection(dbConnection);
    private bool _disposed;

    public IDbQueryable Query(string entity, bool includeArchive = false)
    {
        var queryProvider = new DbQueryProvider(metaProvider, dbConnection, _sqlDialect, entity, includeArchive);

        return new DbQueryable(queryProvider);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;

            if (disposing)
                dbConnection.Dispose();
        }
    }
}

public class DynamicSqlServerContext(IDbConnection dbConnection, IMetaProvider metaProvider) 
    : DynamicSqlContext(dbConnection, metaProvider, new SqlServerDialect())
{ }

public class DynamicSqliteContext(IDbConnection dbConnection, IMetaProvider metaProvider)
    : DynamicSqlContext(dbConnection, metaProvider, new SqliteDialect())
{ }

public class DynamicPostgresContext(IDbConnection dbConnection, IMetaProvider metaProvider)
    : DynamicSqlContext(dbConnection, metaProvider, new PostgresDialect())
{ }
