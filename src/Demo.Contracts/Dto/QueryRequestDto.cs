using Namh.Data.DynamicSql;

namespace Demo.Contracts.Dto;

public sealed record QueryRequestDto(
    string Name,
    List<ProjectionDefinition> Projection,
    FilterDefinition? Filters,
    List<List<SortDefinition>>? Sorts,
    int? Page,
    int? PageSize,
    bool IsBuiltin)
{
    public required string Entity { get; init; }
    public List<EntityContextDefinition> EntityAliases { get; init; } = [];
}