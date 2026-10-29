using Demo.Contracts.Dto;
using Demo.Contracts.Services;
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace Demo.DbServices.DbInstances;

public abstract class DbInstance 
{
    private static HashSet<string> _initializeDbs = [];
    private static object _initializationLock = new();

    protected readonly string ConnectionString;
    protected abstract string? SqlScriptCheckIfDbExists { get; }
    protected abstract string SqlGetTableNames { get; }
    protected abstract string SqlGetColumns { get; }
    protected abstract string SqlGetRelationships { get; }
    protected virtual string IdentifierQuotePrefix => "\"";
    protected virtual string IdentifierQuoteSuffix => "\"";

    protected DbInstance(string connectionString, string[] scriptFields)
    {
        ConnectionString = connectionString;
        lock (_initializationLock)
        {
        if (_initializeDbs.Add(connectionString))
                EnsureDatabaseExists(scriptFields);
        }
    }

    public abstract IDbConnection CreateDbConnection();

    protected virtual void EnsureDatabaseExists(string[] scriptFiles)
    {
        if (string.IsNullOrEmpty(SqlScriptCheckIfDbExists))
            return;

        using var cn = CreateDbConnection();
        if (ExecuteScalar<long>(cn, SqlScriptCheckIfDbExists) != 1)
        {
            foreach (var fileName in scriptFiles)
            {
                var filePath = Path.Combine(Environment.CurrentDirectory, fileName);
                var scripts = File.ReadAllText(filePath);
                ExecuteNonQuery(cn, scripts);
            }
        }
    }
    public virtual string[] GetTableNames()
    {
        using var connection = CreateDbConnection();
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
        using var connection = CreateDbConnection();
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

    public DatabaseTableDataDto GetTableData(string tableName, string? schema = null, int? limit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "The row limit must be greater than zero.");
        var rowLimit = Math.Min(limit ?? 10, 10);

        var matchingTables = GetDatabaseSchema().Tables.Where(table =>
            string.Equals(table.Name, tableName, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(schema)
                || string.Equals(table.Schema, schema, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (matchingTables.Length == 0)
            throw new KeyNotFoundException($"Table '{tableName}' was not found.");
        if (matchingTables.Length > 1)
            throw new ArgumentException($"Table name '{tableName}' is ambiguous. Specify a schema.", nameof(tableName));

        var selectedTable = matchingTables[0];
        using var connection = CreateDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();
        using var command = connection.CreateCommand();
        var qualifiedName = QuoteIdentifier(selectedTable.Name);
        if (!string.IsNullOrWhiteSpace(selectedTable.Schema))
            qualifiedName = $"{QuoteIdentifier(selectedTable.Schema)}.{qualifiedName}";
        command.CommandText = $"SELECT * FROM {qualifiedName}";
        using var reader = command.ExecuteReader();

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();
        var rows = new List<IReadOnlyList<string>>();
        while (rows.Count < rowLimit && reader.Read())
        {
            var row = new string[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
                row[index] = reader.IsDBNull(index)
                    ? string.Empty
                    : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty;
            rows.Add(row);
        }

        return new DatabaseTableDataDto(selectedTable.Name, columns, rows);
    }

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

    private string QuoteIdentifier(string identifier)
        => $"{IdentifierQuotePrefix}{identifier.Replace(IdentifierQuoteSuffix, IdentifierQuoteSuffix + IdentifierQuoteSuffix, StringComparison.Ordinal)}{IdentifierQuoteSuffix}";

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
