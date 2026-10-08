using Namh.Data.DynamicSql.Internal.Query;
using System.Data;

namespace Namh.Data.DynamicSql;

public class DynamicSqlContext : IDisposable
{
    private readonly IDbConnection _dbConnection;
    private readonly IMetaProvider _metaProvider;
    private readonly ISqlDialect _sqlDialect;
    private bool _disposed;

    public DynamicSqlContext(
        IDbConnection dbConnection,
        IMetaProvider metaProvider,
        ISqlDialect? sqlDialect = null)
    {
        _dbConnection = dbConnection;
        _metaProvider = metaProvider;
        _sqlDialect = sqlDialect ?? SqlDialect.ForConnection(dbConnection);
    }

    public IDbQueryable Query(string entity, bool includeArchive = false)
    {
        var queryProvider = new DbQueryProvider(_metaProvider, _dbConnection, _sqlDialect, entity, includeArchive);

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
                _dbConnection.Dispose();
        }
    }
}
