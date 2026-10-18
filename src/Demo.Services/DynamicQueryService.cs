using Demo.Contracts.Repositories;
using Demo.Contracts.Services;
using Microsoft.Extensions.Logging;
using Namh.Data.DynamicSql;

namespace Demo.Services;

public sealed class DynamicQueryService(
    IDbConnectionFactory connectionFactory,
    IMetadataService metadataService,
    ILogger<DynamicQueryService> logger) : IDynamicQueryService
{
    public string GetSqlQueryString(QueryDefinition query)
    {
        using var connection = connectionFactory.CreateConnection();
        using var context = new DynamicSqlContext(connection, metadataService.GetMetaProvider());
        return context.Query(query).ToSql().CommandText;
    }

    public async Task<TableDataDto> ExecuteQueryAsync(
        QueryDefinition query,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var context = new DynamicSqlContext(connection, metadataService.GetMetaProvider());
        var result = await context.Query(query).ToListWithTotalRecordsAsync(cancellationToken);

        logger.LogInformation("\n{query}\n", query);
        logger.LogInformation("Dynamic query returned {TotalRecords} total records.", result.TotalRecords);

        return new TableDataDto(
            Name: query.Entity,
            Columns: string.Join(",", query.Fields.Select(field => field.Alias ?? field.Field.ToText())),
            Rows: result.Records.Select(row => string.Join(",", row.Select(value => value.Value))).ToArray(),
            Page: query.Page,
            PageSize: query.PageSize,
            TotalRecords: result.TotalRecords);
    }
}
