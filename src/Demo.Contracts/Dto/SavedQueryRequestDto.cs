using Namh.Data.DynamicSql;

namespace Demo.Contracts.Dto;

public sealed record SavedQueryRequestDto(
    string Name,
    QueryDefinition Query,
    bool IsBuiltin);
