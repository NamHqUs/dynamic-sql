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
        var result = await context.Query(query).ToListWithTotalRecordsAsync();

        Console.WriteLine($"\nTotal Records: {result.TotalRecords}\n");

        return new TableDataDto(
            Name: query.Entity,
            Columns: string.Join(",", query.Fields.Select(field => field.Alias ?? field.Field.ToText())),
            Rows: result.Records.Select(row => string.Join(",", row.Select(v => v.Value))).ToArray(),
            Page: query.Page,
            PageSize: query.PageSize,
            TotalRecords: result.TotalRecords);
    }
}   
