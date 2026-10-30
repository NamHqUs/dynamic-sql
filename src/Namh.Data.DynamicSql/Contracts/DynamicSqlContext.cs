using Namh.Data.DynamicSql.Execution;
using System.Data;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public class DynamicSqlContext(
    IDbConnection dbConnection,
    IMetadataProvider metaProvider,
    IFunctionProvider functionProvider,
    ISqlDialect? sqlDialect = null) : IDisposable
{
    private readonly ISqlDialect _sqlDialect = sqlDialect ?? SqlDialect.ForConnection(dbConnection);
    private bool _disposed;

    public DynamicSqlContext(IDbConnection dbConnection, IMetadataProvider metaProvider, ISqlDialect? sqlDialect = null)
        : this(dbConnection, metaProvider, new FunctionProvider(), sqlDialect)
    {
    }

    public DynamicSqlContext(IDbConnection dbConnection, IEnumerable<Entity> entities, IEnumerable<SqlFunctionDefintion>? functions = null, ISqlDialect? sqlDialect = null):
        this(dbConnection, new MetaProvider(entities), new FunctionProvider(functions), sqlDialect)
    {
    }

    public IDbQueryable Query(string entity)
    {
        var queryProvider = new DbQueryProvider(
            metaProvider,
            functionProvider,
            dbConnection,
            _sqlDialect,
            entity);

        return new DbQueryable(queryProvider);
    }

    public IDbQueryable QueryYml(string yml)
        => DbQueryFromQueryDto.BuildQueryable(this, yml);

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

public class DynamicSqlServerContext(IDbConnection dbConnection, IMetadataProvider metaProvider, IFunctionProvider functionProvider)
    : DynamicSqlContext(dbConnection, metaProvider, functionProvider, new SqlServerDialect())
{
    public DynamicSqlServerContext(IDbConnection dbConnection, IMetadataProvider metaProvider)
        : this(dbConnection, metaProvider, new FunctionProvider())
    {
    }
}

public class DynamicSqliteContext(IDbConnection dbConnection, IMetadataProvider metaProvider, IFunctionProvider functionProvider)
    : DynamicSqlContext(dbConnection, metaProvider, functionProvider, new SqliteDialect())
{
    public DynamicSqliteContext(IDbConnection dbConnection, IMetadataProvider metaProvider)
        : this(dbConnection, metaProvider, new FunctionProvider())
    {
    }
}

public class DynamicPostgresContext(IDbConnection dbConnection, IMetadataProvider metaProvider, IFunctionProvider functionProvider)
    : DynamicSqlContext(dbConnection, metaProvider, functionProvider, new PostgresDialect())
{
    public DynamicPostgresContext(IDbConnection dbConnection, IMetadataProvider metaProvider)
        : this(dbConnection, metaProvider, new FunctionProvider())
    {
    }
}
