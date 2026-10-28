using Demo.Contracts.Repositories;
using Demo.Contracts.Dto;
using System.Data;

namespace Demo.Repositories;

public sealed class RawDataRepository(IDbConnectionFactory connectionFactory) : IRawDataRepository
{
    private const int MaxTableDataRows = 10;

    public string[] GetExposedTables() => connectionFactory.GetTableNames();

    public DatabaseSchemaDto GetDatabaseSchema() => connectionFactory.GetDatabaseSchema();

    public TableDataDto GetTableData(string tableName, string? schema = null, int? limit = null)
    {
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "The row limit must be greater than zero.");
        var rowLimit = Math.Min(limit ?? MaxTableDataRows, MaxTableDataRows);

        var exposedTables = connectionFactory.GetTableNames();
        var exposedTable = exposedTables.FirstOrDefault(name =>
            string.Equals(name, tableName, StringComparison.Ordinal));
        if (exposedTable is null)
        {
            var caseInsensitiveMatches = exposedTables
                .Where(name => string.Equals(name, tableName, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();
            if (caseInsensitiveMatches.Length == 1)
                exposedTable = caseInsensitiveMatches[0];
        }

        if (exposedTable is null)
            throw new KeyNotFoundException($"Table '{tableName}' was not found.");

        using var connection = connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();
        using var command = connection.CreateCommand();
        var qualifiedName = connectionFactory.QuoteIdentifier(exposedTable);
        if (!string.IsNullOrWhiteSpace(schema))
        {
            var schemaExists = connectionFactory.GetDatabaseSchema().Tables.Any(table =>
                string.Equals(table.Schema, schema, StringComparison.OrdinalIgnoreCase)
                && string.Equals(table.Name, exposedTable, StringComparison.OrdinalIgnoreCase));
            if (!schemaExists)
                throw new KeyNotFoundException($"Table '{schema}.{tableName}' was not found.");
            qualifiedName = $"{connectionFactory.QuoteIdentifier(schema)}.{qualifiedName}";
        }
        command.CommandText = $"SELECT * FROM {qualifiedName}";
        using var reader = command.ExecuteReader();

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();

        var rows = new List<string>();
        while (rows.Count < rowLimit && reader.Read())
        {
            var row = new string[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
                row[index] = reader[index].ToString()!;
            rows.Add(string.Join(",", row));
        }

        return new TableDataDto(exposedTable, string.Join(",", columns), rows.ToArray(), 0, rowLimit, rows.Count);
    }
}
