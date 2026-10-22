using Demo.Contracts.Repositories;
using System.Data;

namespace Demo.Repositories;

public sealed class RawDataRepository(IDbConnectionFactory connectionFactory) : IRawDataRepository
{
    public string[] GetExposedTables() => connectionFactory.GetTableNames();

    public TableDataDto GetTableData(string tableName)
    {
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
        command.CommandText = $"SELECT * FROM {connectionFactory.QuoteIdentifier(exposedTable)}";
        using var reader = command.ExecuteReader();

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();

        var rows = new List<string>();
        while (reader.Read())
        {
            var row = new string[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
                row[index] = reader[index].ToString()!;
            rows.Add(string.Join(",", row));
        }

        return new TableDataDto(exposedTable, string.Join(",", columns), rows.ToArray(), 0, 0, rows.Count);
    }
}
