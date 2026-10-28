using Microsoft.Data.Sqlite;
using System.Data;
using System.Data.Common;
using Demo.Contracts.Dto;

namespace Demo.Repositories.DataProviders;

public class SqliteDbProvider : DbProviderBase
{
    private readonly string _connectionString;
    private readonly bool _isInMemory;
    private readonly object _keepAliveLock = new();
    private SqliteConnection? _keepAliveConnection;

    public SqliteDbProvider(string cnString, string[] scriptFiles) : base(scriptFiles)
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder(cnString);
        _isInMemory = connectionStringBuilder.DataSource == ":memory:";
        if (_isInMemory)
        {
            connectionStringBuilder.DataSource = $"DynamicSqlDemo_{Guid.NewGuid():N}";
            connectionStringBuilder.Mode = SqliteOpenMode.Memory;
            connectionStringBuilder.Cache = SqliteCacheMode.Shared;
        }

        _connectionString = connectionStringBuilder.ToString();
    }

    protected override string? SqlScriptCheckIfDbExists
        => "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Customers')";

    protected override string SqlGetTableNames
        => "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT GLOB 'sqlite_*' ORDER BY name;";

    protected override string SqlGetColumns => "SELECT NULL AS SchemaName, NULL AS TableName, NULL AS ColumnName, NULL AS DataType, 0 AS IsPrimaryKey WHERE 1 = 0;";
    protected override string SqlGetRelationships => "SELECT NULL AS RelationName, NULL AS SourceSchema, NULL AS SourceTable, NULL AS SourceColumn, NULL AS TargetSchema, NULL AS TargetTable, NULL AS TargetColumn, 0 AS Ordinal, 0 AS IsUnique WHERE 1 = 0;";

    public override DatabaseSchemaDto GetDatabaseSchema()
    {
        using var connection = GetDbConnection();
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

    protected override IDbConnection CreateDbConnection()
    {
        if (_isInMemory)
        {
            lock (_keepAliveLock)
            {
                if (_keepAliveConnection is null)
                {
                    _keepAliveConnection = new SqliteConnection(_connectionString);
                    _keepAliveConnection.Open();
                }
            }
        }

        return new SqliteConnection(_connectionString);
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
}
