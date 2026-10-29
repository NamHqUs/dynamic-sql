using Microsoft.Data.Sqlite;
using Demo.Contracts.Dto;
using System.Data;
using System.Data.Common;

namespace Demo.DbServices.DbInstances;

public sealed class SqliteDbInstance : DbInstance, IDisposable
{
    private readonly object _keepAliveLock = new();

    private bool? _inMemory;
    private bool InMemory => _inMemory ??= ConnectionString.Contains(":memory:");
    private SqliteConnection? _keepAliveConnection;

    public SqliteDbInstance(string connectionString, string[] scriptFiles)
        : base( DbServerType.Sqlite, NormalizeConnectionString(connectionString), scriptFiles)
    {
    }

    protected override string? SqlScriptCheckIfDbExists
        => "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Customers')";

    protected override string SqlGetTableNames
        => "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT GLOB 'sqlite_*' ORDER BY name;";

    protected override string SqlGetColumns => "SELECT NULL AS SchemaName, NULL AS TableName, NULL AS ColumnName, NULL AS DataType, 0 AS IsPrimaryKey WHERE 1 = 0;";
    protected override string SqlGetRelationships => "SELECT NULL AS RelationName, NULL AS SourceSchema, NULL AS SourceTable, NULL AS SourceColumn, NULL AS TargetSchema, NULL AS TargetTable, NULL AS TargetColumn, 0 AS Ordinal, 0 AS IsUnique WHERE 1 = 0;";

    public override bool TestConnection()
    {
        if (!InMemory)
            return base.TestConnection();

        try
        {
            var cn = CreateDbConnection();
            if (cn.State != ConnectionState.Open)
                cn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }
    public override DatabaseSchemaDto GetDatabaseSchema()
    {
        using var connection = CreateDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();
        var dbConnection = connection as DbConnection
            ?? throw new InvalidOperationException("The configured database connection does not support schema discovery.");

        var tables = new List<DatabaseTableDto>();
        var relationships = new List<DatabaseRelationshipDto>();
        foreach (var tableName in GetTableNames())
        {
            var columns = ReadSchemaRows(dbConnection, $"PRAGMA table_info({QuoteLiteral(tableName)});");
            var tableColumns = columns.Rows.Cast<DataRow>()
                .Select(row => new DatabaseColumnDto(
                    row["name"]?.ToString() ?? string.Empty,
                    row["type"]?.ToString() ?? string.Empty,
                    Convert.ToInt32(row["pk"]) > 0))
                .ToArray();
            tables.Add(new DatabaseTableDto("main", tableName, tableColumns));

            var uniqueColumnSets = GetUniqueColumnSets(dbConnection, tableName);
            var foreignKeys = ReadSchemaRows(dbConnection, $"PRAGMA foreign_key_list({QuoteLiteral(tableName)});");
            foreach (var foreignKey in foreignKeys.Rows.Cast<DataRow>().GroupBy(row => Convert.ToInt32(row["id"])))
            {
                var orderedColumns = foreignKey.OrderBy(row => Convert.ToInt32(row["seq"])).ToArray();
                var sourceColumns = orderedColumns.Select(row => row["from"]?.ToString() ?? string.Empty).ToArray();
                var targetColumns = orderedColumns.Select(row => row["to"]?.ToString() ?? string.Empty).ToArray();
                relationships.Add(new DatabaseRelationshipDto(
                    $"FK_{tableName}_{foreignKey.Key}",
                    "main",
                    tableName,
                    sourceColumns,
                    "main",
                    orderedColumns[0]["table"]?.ToString() ?? string.Empty,
                    targetColumns,
                    uniqueColumnSets.Any(uniqueColumns => uniqueColumns.SetEquals(sourceColumns))));
            }
        }

        return new DatabaseSchemaDto(tables, relationships);
    }

    public override IDbConnection CreateDbConnection()
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder(ConnectionString);
        if (connectionStringBuilder.Mode != SqliteOpenMode.Memory)
            return new SqliteConnection(ConnectionString);

        lock (_keepAliveLock)
        {
            if (_keepAliveConnection?.State != ConnectionState.Open)
            {
                _keepAliveConnection?.Dispose();
                _keepAliveConnection = new SqliteConnection(ConnectionString);
                _keepAliveConnection.Open();
            }
        }

        return new SqliteConnection(ConnectionString);
    }

    public void Dispose()
    {
        lock (_keepAliveLock)
        {
            _keepAliveConnection?.Dispose();
            _keepAliveConnection = null;
        }
    }

    private HashSet<string>[] GetUniqueColumnSets(DbConnection connection, string tableName)
    {
        var indexes = ReadSchemaRows(connection, $"PRAGMA index_list({QuoteLiteral(tableName)});");
        return indexes.Rows.Cast<DataRow>()
            .Where(row => Convert.ToInt32(row["unique"]) == 1)
            .Select(row =>
            {
                var indexName = row["name"]?.ToString() ?? string.Empty;
                var columns = ReadSchemaRows(connection, $"PRAGMA index_info({QuoteLiteral(indexName)});");
                return columns.Rows.Cast<DataRow>()
                    .Select(column => column["name"]?.ToString() ?? string.Empty)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            })
            .ToArray();
    }

    private static string QuoteLiteral(string value)
        => $"'{value.Replace("'", "''")}'";

    private static string NormalizeConnectionString(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (builder.Mode == SqliteOpenMode.Memory || builder.DataSource == ":memory:")
        {
            if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
                builder.DataSource = $"DynamicSql-{Guid.NewGuid():N}";
            builder.Mode = SqliteOpenMode.Memory;
            builder.Cache = SqliteCacheMode.Shared;
        }
        return builder.ToString();
    }
}
