using Namh.Data.DynamicSql.Internal.Query.Expressions;
using Namh.Data.DynamicSql.Internal.Query.Models;
using Namh.Data.DynamicSql.Internal.Query.Visitors;
using Namh.Data.DynamicSql.Model;
using System.Data;
using System.Globalization;
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

    public SqlQuery GetQuery(Expression expression)
    {
        var (sqlQuery, parameters, _) = Translate(expression);
        var parameterList = parameters.ToArray();
        var commandText = SqlFormatter.Format(sqlQuery);

        if (parameterList.Length != 0)
        {
            commandText = sqlDialect is SqlServerDialect
                ? FormatParameterDeclarations(parameterList) + "\n" + commandText
                : commandText + $"\n-- Parameters: {string.Join(", ", parameterList.Select(FormatParameter))}";
        }

        return new SqlQuery(
            commandText,
            parameterList.Select(e => new SqlQueryParameter(e.Name, e.Value)).ToArray());
    }

    private static string FormatParameter(DbParameter parameter)
        => $"{parameter.Name} = {FormatParameterValue(parameter.Value)}";

    private static string FormatParameterDeclarations(IReadOnlyList<DbParameter> parameters)
    {
        var declarations = parameters
            .Select(FormatParameterAssignment)
            .ToArray();

        return declarations.Length == 1
            ? "DECLARE " + declarations[0] + ";"
            : "DECLARE " + declarations[0] + ",\n\t" +
                string.Join(",\n\t", declarations.Skip(1)) + ";";
    }

    private static string FormatParameterAssignment(DbParameter parameter)
        => $"{parameter.Name} {GetSqlServerType(parameter.Value)} = {FormatParameterValue(parameter.Value)}";

    private static string GetSqlServerType(object? value)
        => value switch
        {
            null => "nvarchar(4000)",
            string => "nvarchar(4000)",
            bool => "bit",
            byte or short or int or long => "int",
            float or double => "float",
            decimal => "decimal(38, 18)",
            DateTime or DateTimeOffset => "datetime2",
            Guid => "uniqueidentifier",
            _ => "nvarchar(4000)"
        };

    private static string FormatParameterValue(object? value)
        => value switch
        {
            null => "NULL",
            string text => $"'{text.Replace("'", "''", StringComparison.Ordinal)}'",
            bool boolean => boolean ? "1" : "0",
            DateTime dateTime => $"'{dateTime.ToString("O", CultureInfo.InvariantCulture)}'",
            DateTimeOffset dateTimeOffset => $"'{dateTimeOffset.ToString("O", CultureInfo.InvariantCulture)}'",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "NULL",
            _ => $"'{value}'"
        };

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
        var query = GetQuery(expression);
        var sb = new StringBuilder(SqlFormatter.Format(query.CommandText));

        foreach (var parameter in query.Parameters)
        {
            sb.AppendLine();
            sb.AppendFormat("{0} = {1} (Type:{2})",
                parameter.Name,
                parameter.Value ?? "[NULL]",
                parameter.Value?.GetType().Name ?? "UNKNOWN");
        }

        return sb.ToString();
    }

    private (
        string sqlQuery, 
        IEnumerable<DbParameter> parameters, 
        IEnumerable<ProjectionItem> projectionItems) 
        Translate(Expression expression)
    {
        var queryContext = new QueryContext(metaProvider, includeArchive);
        var translator =  (TableExpression) new QueryVisitor(queryContext).Visit(expression);

        var rendered = new SqlGenerator(sqlDialect).Generate(translator, queryContext.DbParameters);
        return (rendered.CommandText, rendered.Parameters, translator.ProjectionItems);
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
