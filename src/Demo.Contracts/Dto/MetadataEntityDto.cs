using Namh.Data.DynamicSql;

namespace Demo.Contracts.Dto;

public sealed record MetadataEntityDto(
    string Name,
    List<IField> Fields,
    string RawSql,
    List<Relation>? Relations,
    bool CanDelete)
{
    public static MetadataEntityDto FromEntity(Entity entity, bool canDelete)
        => new(entity.Name, entity.Fields, entity.RawSql, entity.Relations, canDelete);
}


