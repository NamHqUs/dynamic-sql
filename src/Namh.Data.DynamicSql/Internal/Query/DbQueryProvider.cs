using Namh.Data.DynamicSql.Internal.Query.Expressions;
using Namh.Data.DynamicSql.Internal.Query.Models;
using Namh.Data.DynamicSql.Internal.Query.Visitors;
using Namh.Data.DynamicSql.Model;
using System.Data;
using DbCommand = System.Data.Common.DbCommand;
using DbConnection = System.Data.Common.DbConnection;
using System.Linq.Expressions;
using System.Text;

namespace Namh.Data.DynamicSql.Internal.Query;

class DbQueryProvider(
    IMetaProvider metaProvider,
    IDbConnection dbConnection,
    ISqlDialect sqlDialect,
    string entityName,
    bool includeArchive) : IDbQueryProvider
{
    public readonly Entity Entity = metaProvider.Get(entityName);

    public IDbQueryable CreateQuery(Expression expression)
        => new DbQueryable(this, expression);

    public IEnumerable<DataRecord> Execute(Expression expression)
    {
        var (sqlQuery, parameters, projection) = Translate(expression);

        var cmd = CreateDbCommand(sqlQuery, parameters);
        return new DbReader(cmd.ExecuteReader(), projection);
    }

    public object? ExecuteScalar(Expression expression)
    {
        var (sqlQuery, parameters, _) = Translate(expression);

        var cmd = CreateDbCommand(sqlQuery, parameters);
        return cmd.ExecuteScalar();
    }

    public async Task<List<DataRecord>> ExecuteAsync(Expression expression, CancellationToken cancellationToken)
    {
        var (sqlQuery, parameters, projection) = Translate(expression);
        var connection = GetAsyncConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        using var cmd = CreateDbCommand(sqlQuery, parameters);
        if (cmd is not DbCommand dbCommand)
            throw new InvalidOperationException(
                $"The database command type [{cmd.GetType().FullName}] does not support asynchronous execution.");

        await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
        return await DbReader.ReadAllAsync(reader, projection, cancellationToken);
    }

    public async Task<object?> ExecuteScalarAsync(Expression expression, CancellationToken cancellationToken)
    {
        var (sqlQuery, parameters, _) = Translate(expression);
        var connection = GetAsyncConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        using var cmd = CreateDbCommand(sqlQuery, parameters);
        if (cmd is not DbCommand dbCommand)
            throw new InvalidOperationException(
                $"The database command type [{cmd.GetType().FullName}] does not support asynchronous execution.");

        return await dbCommand.ExecuteScalarAsync(cancellationToken);
    }

    public string GetQueryText(Expression expression)
    {
        var (sqlQuery, parameters, _) = Translate(expression);
        var sb = new StringBuilder(sqlQuery);

        if (parameters != null)
        {
            sb.AppendLine();
            foreach (var e in parameters)
            {
                sb.AppendLine();
                sb.AppendFormat("{0} = {1} (Type:{2})", e.Name, e.Value ?? "[NULL]", e.Value?.GetType().Name ?? "UNKNOW");
            }
        }

        return sb.ToString();
    }

    private (
        string sqlQuery, 
        IEnumerable<DbParameter> parameters, 
        IEnumerable<ProjectionItem> projectionItems) 
        Translate(Expression expression)
    {
        var queryContext = new QueryContext(metaProvider, sqlDialect, includeArchive);
        var translator =  (TableExpression) new QueryVisitor(queryContext).Visit(expression);

        return (translator.ToString(), queryContext.DbParameters, translator.ProjectionItems);
    }

    private IDbCommand CreateDbCommand(string sqlQuery, IEnumerable<DbParameter> parameters)
    {
        if (dbConnection.State != ConnectionState.Open)
            dbConnection.Open();

        var cmd = dbConnection.CreateCommand();
        cmd.CommandText = sqlQuery;

        foreach (var e in parameters)
        {
            var para = cmd.CreateParameter();
            para.ParameterName = e.Name;
            para.Value = e.Value;
            cmd.Parameters.Add(para);
        }

        return cmd;
    }

    private DbConnection GetAsyncConnection()
        => dbConnection as DbConnection
            ?? throw new InvalidOperationException(
                $"The database connection type [{dbConnection.GetType().FullName}] does not support asynchronous execution.");
}
