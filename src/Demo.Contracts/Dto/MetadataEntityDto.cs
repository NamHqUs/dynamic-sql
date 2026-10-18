using Namh.Data.DynamicSql;

namespace Demo.Contracts.Dto;

public sealed record MetadataEntityDto(
    string Name,
    string RawSql,
    List<Field> Fields,
    List<Relation>? Relations,
    bool IsBuiltin);
