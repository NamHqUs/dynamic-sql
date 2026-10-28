using System.Data;
using System.Data.Common;
using Demo.Contracts.Dto;

namespace Demo.Repositories.DataProviders;

public abstract class DbProviderBase(string[] scriptFiles)
{
    private static readonly object oLock = new();
    private static bool _initialized = false;

    protected abstract string? SqlScriptCheckIfDbExists { get; }
    protected abstract string SqlGetTableNames { get; }
    protected abstract string SqlGetColumns { get; }
    protected abstract string SqlGetRelationships { get; }
    protected virtual string IdentifierQuotePrefix => "\"";
    protected virtual string IdentifierQuoteSuffix => "\"";

    public virtual IDbConnection GetDbConnection()
    {
        var cn = CreateDbConnection();
        lock (oLock)
        {
            if (!_initialized  
                && (string.IsNullOrEmpty(SqlScriptCheckIfDbExists) 
                        || ExecuteScalar<long>(cn, SqlScriptCheckIfDbExists) != 1))
                    InitializeDb(cn);

            _initialized = true;
        }
        return cn;
    }

    public string[] GetTableNames()
    {
        using var connection = GetDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = SqlGetTableNames;
        using var reader = command.ExecuteReader();
        var tableNames = new List<string>();
        while (reader.Read())
            tableNames.Add(reader.GetString(0));

        return tableNames.ToArray();
    }

    public virtual DatabaseSchemaDto GetDatabaseSchema()
    {
        using var connection = GetDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        var dbConnection = connection as DbConnection
            ?? throw new InvalidOperationException("The configured database connection does not support schema discovery.");

        var columns = ReadSchemaRows(dbConnection, SqlGetColumns);
        var relationships = ReadSchemaRows(dbConnection, SqlGetRelationships);
        var tables = columns.Rows
            .Cast<DataRow>()
            .GroupBy(row => (Schema: Value(row, "SchemaName"), Name: Value(row, "TableName")))
            .Select(group => new DatabaseTableDto(
                group.Key.Schema,
                group.Key.Name,
                group.Select(row => new DatabaseColumnDto(
                    Value(row, "ColumnName"),
                    Value(row, "DataType"),
                    Convert.ToBoolean(row["IsPrimaryKey"])))
                    .ToArray()))
            .ToArray();

        var relationDtos = relationships.Rows
            .Cast<DataRow>()
            .GroupBy(row => Value(row, "RelationName"), StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                return new DatabaseRelationshipDto(
                    Value(first, "RelationName"),
                    Value(first, "SourceSchema"),
                    Value(first, "SourceTable"),
                    group.OrderBy(row => Convert.ToInt32(row["Ordinal"]))
                        .Select(row => Value(row, "SourceColumn"))
                        .ToArray(),
                    Value(first, "TargetSchema"),
                    Value(first, "TargetTable"),
                    group.OrderBy(row => Convert.ToInt32(row["Ordinal"]))
                        .Select(row => Value(row, "TargetColumn"))
                        .ToArray(),
                    Convert.ToBoolean(first["IsUnique"]));
            })
            .ToArray();

        return new DatabaseSchemaDto(tables, relationDtos);
    }

    public string QuoteIdentifier(string identifier)
    {
        var escapedIdentifier = identifier.Replace(IdentifierQuoteSuffix, IdentifierQuoteSuffix + IdentifierQuoteSuffix);
        return $"{IdentifierQuotePrefix}{escapedIdentifier}{IdentifierQuoteSuffix}";
    }

    protected abstract IDbConnection CreateDbConnection();

    protected virtual DataTable ReadSchemaRows(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new DataTable();
        rows.Load(reader);
        return rows;
    }

    private static string Value(DataRow row, string columnName)
        => row[columnName]?.ToString() ?? string.Empty;
    private void InitializeDb(IDbConnection cn)
    {
        foreach (var fileName in scriptFiles)
        {
            var filePath = Path.Combine(Environment.CurrentDirectory, fileName);
            var scripts = File.ReadAllText(filePath);
            ExecuteNonQuery(cn, scripts);
        }
    }

    private static T ExecuteScalar<T>(IDbConnection cn, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;

        if (cn.State != ConnectionState.Open)
            cn.Open();
        return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T));
    }
    private static void ExecuteNonQuery(IDbConnection cn, string sql)
    {
        using var cmd = cn.CreateCommand();
        if (cn.State != ConnectionState.Open)
            cn.Open();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
