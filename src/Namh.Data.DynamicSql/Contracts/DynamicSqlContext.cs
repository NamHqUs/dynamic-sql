using Namh.Data.DynamicSql.Execution;
using System.Data;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public class DynamicSqlContext(
    IDbConnection dbConnection,
    IMetaProvider metaProvider,
    ISqlDialect? sqlDialect = null) : IDisposable
{
    private readonly ISqlDialect _sqlDialect = sqlDialect ?? SqlDialect.ForConnection(dbConnection);
    private bool _disposed;

    public DynamicSqlContext(IDbConnection dbConnection, IEnumerable<Entity> entities, IEnumerable<SqlFunctionDefintion>? functions = null, ISqlDialect? sqlDialect = null):
        this(dbConnection, new MetaProvider(entities, functions), sqlDialect)
    {
    }

    public IDbQueryable Query(
        string entity,
        bool includeArchive = false,
        IReadOnlyList<EntityContextDefinition>? entityAliases = null)
    {
        var queryProvider = new DbQueryProvider(
            metaProvider,
            dbConnection,
            _sqlDialect,
            entity,
            includeArchive,
            entityAliases ?? []);

        return new DbQueryable(queryProvider);
    }

    public IDbQueryable QueryJson(string json)
        => DbQueryFromQueryDto.BuildQueryable(this, json);

    public IDbQueryable Query(QueryDefinition queryRequest)
        => DbQueryFromQueryDto.BuildQueryable(this, queryRequest);


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
    : DynamicSqlContext(dbConnection, metaProvider, new SqlServerDialect());

public class DynamicSqliteContext(IDbConnection dbConnection, IMetaProvider metaProvider)
    : DynamicSqlContext(dbConnection, metaProvider, new SqliteDialect());

public class DynamicPostgresContext(IDbConnection dbConnection, IMetaProvider metaProvider)
    : DynamicSqlContext(dbConnection, metaProvider, new PostgresDialect());
