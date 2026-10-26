using Namh.Data.DynamicSql;

namespace Demo.Contracts.Dto;

public sealed record QueryContextDto(
    string Name,
    string Entity,
    List<ProjectionDefinition> Projection,
    ConditionDefinition? Filters,
    List<List<SortDefinition>>? Sorts,
    int? Page,
    int? PageSize)
{
    public string Id { get; init; } = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString("N")[..8]}";
    public List<JoinedQueryContextDto>? JoinedContexts { get; init; }
}

public sealed record JoinedQueryContextDto(
    string RefContextId,
    string Alias,
    IConditionDefintion? Condition,
    QueryJoinType JoinType = QueryJoinType.Left);

public sealed record QueryDto(
    QueryContextDto MainQuery,
    List<QueryContextDto>? Contexts,
    bool IsBuiltin)
{
}