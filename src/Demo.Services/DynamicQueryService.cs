using Demo.Contracts.Services;
using Demo.Contracts.Exceptions;
using Microsoft.Extensions.Logging;
using Namh.Data.DynamicSql;
using System.Data.Common;

namespace Demo.Services;

public sealed class DynamicQueryService(
    IDbConnectionService dbConnectionService,
    IMetadataService metadataService,
    IOperatorDefinitionService operatorDefinitionService,
    ILogger<DynamicQueryService> logger) : IDynamicQueryService
{
    public string GetSqlQueryString(QueryDefinition query)
    {
        try
        {
            using var connection = dbConnectionService.CreateConnection();
            using var context = new DynamicSqlContext(
                connection,
                metadataService.GetMetaProvider(),
                metadataService.GetFunctionProvider());
            return context.Query(PrepareQuery(query, connection)).ToSql().CommandText;
        }
        catch (DbException exception)
        {
            throw new DatabaseConnectionException(exception);
        }
    }

    public async Task<TableDataDto> ExecuteQueryAsync(
        QueryDefinition query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = dbConnectionService.CreateConnection();
            using var context = new DynamicSqlContext(
                connection,
                metadataService.GetMetaProvider(),
                metadataService.GetFunctionProvider());
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
        catch (DbException exception)
        {
            throw new DatabaseConnectionException(exception);
        }
    }

    private QueryDefinition PrepareQuery(QueryDefinition query, System.Data.IDbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Filter is null && query.Contexts is not { Count: > 0 })
            return query;

        var customOperators = operatorDefinitionService.GetAll()
            .Where(definition => !definition.IsBuiltin)
            .ToDictionary(definition => definition.Id, StringComparer.OrdinalIgnoreCase);
        var provider = SqlDialect.ForConnection(connection).Key;

        ConditionDefinition PrepareGroup(ConditionDefinition group)
            => new(group.LogicalOperation, System.Linq.Enumerable.Select<IConditionDefintion, IConditionDefintion>(
                group.Filters,
                PrepareCondition).ToArray())
            {
                Note = group.Note
            };

        IConditionDefintion PrepareCondition(IConditionDefintion condition)
            => condition switch
            {
                ConditionDefinition nested => PrepareGroup(nested),
                ConditionStatementDefinition { OperatorId: { } id } item =>
                    PrepareOperator(item, id, customOperators, provider),
                ConditionStatementDefinition item => item,
                _ => throw new NotSupportedException(
                    $"Unsupported filter definition [{condition.GetType().Name}].")
            };

        return new QueryDefinition
        {
            Entity = query.Entity,
            Projection = query.Projection,
            Filter = query.Filter is null ? null : PrepareGroup(query.Filter),
            Sorts = query.Sorts,
            Page = query.Page,
            PageSize = query.PageSize,
            Contexts = query.Contexts?
                .Select(context => context with
                {
                    Query = PrepareQuery(context.Query, connection),
                    Condition = context.Condition is null ? null : PrepareCondition(context.Condition)
                })
                .ToList()
        };
    }

    private static ConditionStatementDefinition PrepareOperator(
        ConditionStatementDefinition filter,
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
