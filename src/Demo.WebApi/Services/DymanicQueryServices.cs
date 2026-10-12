using System.Data;
using System.Text.Json;
using Namh.Data.DynamicSql;

namespace Demo.WebApi.Services;

public static class DynamicQueryServices
{
    public static string GetSqlQueryStringAsync(QueryRequest query)
    {
        Console.WriteLine($"\n{query}\n");
        var dbConnection = DbProviderFactory.GetDbConnection();
        var context = new DynamicSqlContext(dbConnection, MetadataServices.GetMetaProvider());
        var result = context.Query(query).ToSql();
        return result.CommandText;
    }

    public static async Task<TableDataDto> ExecuteQueryAsync(QueryRequest query)
    {
        Console.WriteLine($"\n{query}\n");
        var dbConnection = DbProviderFactory.GetDbConnection();
        var context = new DynamicSqlContext(dbConnection, MetadataServices.GetMetaProvider());
        var result = await context.Query(query).ToListAsync();

        var rows = result.Select(row => string.Join(",", row.Select(v => v.Value))).ToArray();
        Console.WriteLine(string.Join("\n", rows));

        return new TableDataDto(
            Name: query.Entity,
            Columns: string.Join(",", query.Columns.Select(c => c.Alias ?? c.Field)),
            Rows: rows);
    }
}   
