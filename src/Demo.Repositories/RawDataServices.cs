using Demo.Contracts.Repositories;
using System.Data;

namespace Demo.Repositories;

public sealed class RawDataRepository(IDbConnectionFactory connectionFactory) : IRawDataRepository
{
    private static readonly string[] ExposedTables = ["Customers", "Invoices", "InvoiceItems", "Products"];

    public string[] GetExposedTables() => [.. ExposedTables];

    public TableDataDto GetTableData(string tableName)
    {
        if (!ExposedTables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
            throw new KeyNotFoundException($"Table '{tableName}' was not found.");

        using var connection = connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {tableName}";
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

        return new TableDataDto(tableName, string.Join(",", columns), rows.ToArray(), 0, 0, rows.Count);
    }
}
