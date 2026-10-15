using System.Data;
using Namh.Data.DynamicSql;

namespace Demo.WebApi.Services;

public static class DynamicQueryServices
{
    public static string GetSqlQueryStringAsync(QueryDto query)
    {
        var dbConnection = DbProviderFactory.GetDbConnection();
        using var context = new DynamicSqlContext(dbConnection, MetadataServices.GetMetaProvider());
        var result = context.Query(query).ToSql();
        return result.CommandText;
    }

    public static async Task<TableDataDto> ExecuteQueryAsync(
        QueryDto query,
        ILoggerFactory loggerFactory)
    {
        var dbConnection = DbProviderFactory.GetDbConnection();
        using var context = new DynamicSqlContext(dbConnection, MetadataServices.GetMetaProvider());
        var result = await context.Query(query).ToListWithTotalRecordsAsync();

        var logger = loggerFactory.CreateLogger(nameof(DynamicQueryServices));
        logger.LogInformation("Dynamic query returned {TotalRecords} total records.", result.TotalRecords);

        return new TableDataDto(
            Name: query.Entity,
            Columns: string.Join(",", query.Fields.Select(field => field.Alias ?? field.Field.ToText())),
            Rows: result.Records.Select(row => string.Join(",", row.Select(v => v.Value))).ToArray(),
            Page: query.Page,
            PageSize: query.PageSize,
            TotalRecords: result.TotalRecords);
    }
}   
