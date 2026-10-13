using System.Data;

namespace Demo.WebApi.Services;

public static class RawDataServices
{
    public static string[] GetExposedTables()
        => ["Customers", "Invoices", "InvoiceItems", "Products"];

    public static TableDataDto GetTableData(string tableName)
    {
        using var cmd = DbProviderFactory.CreateDbCommand($"SELECT * FROM {tableName}");
        using var reader = cmd.ExecuteReader();

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();

        var rows = new List<string>();
        while (reader.Read())
        {
            var row = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
                row[i] = reader[i].ToString()!;
            rows.Add(string.Join(",", row));
        }

        return new TableDataDto(tableName, string.Join(",", columns), rows.ToArray(), 0, 0, rows.Count);
    }
}




