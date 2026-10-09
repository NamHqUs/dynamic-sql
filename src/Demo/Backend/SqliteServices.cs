#!
#:include Shared.cs

using System.Data;

public static class SqliteServices
{
    private static IDbConnection _cn { get; } = DbConnection.Value;
    public static string[] GetExposedTables()
        => [ "Customers", "Invoices", "InvoiceItems", "Products"];

    public static TableDataDto GetTableData(string tableName)
    {
        using var cmd = _cn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {tableName}";
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

        return new TableDataDto(tableName, string.Join(",", columns), rows.ToArray());
    }

}




