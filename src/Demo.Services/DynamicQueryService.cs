using Demo.Contracts.Repositories;
using Demo.Contracts.Services;
using Microsoft.Extensions.Logging;
using Namh.Data.DynamicSql;

namespace Demo.Services;

public sealed class DynamicQueryService(
    IDbConnectionFactory connectionFactory,
    IMetadataService metadataService,
    IOperatorDefinitionService operatorDefinitionService,
    ILogger<DynamicQueryService> logger) : IDynamicQueryService
{
    public string GetSqlQueryString(QueryDefinition query)
    {
        using var connection = connectionFactory.CreateConnection();
        using var context = new DynamicSqlContext(connection, metadataService.GetMetaProvider());
        return context.Query(PrepareQuery(query, connection)).ToSql().CommandText;
    }

    public async Task<TableDataDto> ExecuteQueryAsync(
        QueryDefinition query,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var context = new DynamicSqlContext(connection, metadataService.GetMetaProvider());
        var result = await context.Query(PrepareQuery(query, connection))
            .ToListWithTotalRecordsAsync(cancellationToken);

        logger.LogInformation("\n{query}\n", query);
        logger.LogInformation("Dynamic query returned {TotalRecords} total records.", result.TotalRecords);

        return new TableDataDto(
            Name: query.Entity,
            Columns: string.Join(",", query.Projection.Select(field => field.Alias ?? field.ToText())),
            Rows: result.Records.Select(row => string.Join(",", row.Select(value => value.Value))).ToArray(),
            Page: query.Page,
            PageSize: query.PageSize,
            TotalRecords: result.TotalRecords);
    }

    private QueryDefinition PrepareQuery(QueryDefinition query, System.Data.IDbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Filter is null)
            return query;

        var customOperators = operatorDefinitionService.GetAll()
            .Where(definition => !definition.IsBuiltin)
            .ToDictionary(definition => definition.Id, StringComparer.OrdinalIgnoreCase);
        var provider = SqlDialect.ForConnection(connection).Key;

        FilterDefinition PrepareGroup(FilterDefinition group)
            => new(group.LogicalOperation, System.Linq.Enumerable.Select<IFilterDefintion, IFilterDefintion>(
                group.Filters,
                (Namh.Data.DynamicSql.IFilterDefintion filter) => filter switch
            {
                FilterDefinition nested => PrepareGroup(nested),
                FilterItemDefinition { OperatorId: { } id } item =>
                    PrepareOperator(item, id, customOperators, provider),
                FilterItemDefinition item => item,
                _ => throw new NotSupportedException(
                    $"Unsupported filter definition [{filter.GetType().Name}].")
            }).ToArray())
            {
                Note = group.Note
            };

        return new QueryDefinition
        {
            Entity = query.Entity,
            Relations = query.Relations,
            Projection = query.Projection,
            Filter = PrepareGroup(query.Filter),
            Sorts = query.Sorts,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    private static FilterItemDefinition PrepareOperator(
        FilterItemDefinition filter,
        string operatorId,
        IReadOnlyDictionary<string, Demo.Contracts.Dto.OperatorDefinitionDto> operators,
        string provider)
    {
        if (!operators.TryGetValue(operatorId, out var definition))
            throw new KeyNotFoundException($"Custom operator '{operatorId}' was not found.");
        if (!definition.SqlTemplates.TryGetValue(provider, out var template))
            throw new InvalidOperationException(
                $"Operator '{definition.Name}' has no SQL template for provider '{provider}'.");

        return filter with { CustomSqlTemplate = template };
    }
}
