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
        => new(
            entity.Name,
            entity.Fields,
            entity is RawSqlEntity rawSqlEntity ? rawSqlEntity.RawSql : entity.DbTable ?? entity.Name,
            entity.Relations,
            canDelete);
}
//public sealed record EntityDto(
//    string Id,
//    string Name,
//    string RawSql,
//    List<FieldDto> Fields,
//    bool CanDelete) 
//{
//    public EntityDto(string name, string rawSql, List<FieldDto> fields,  bool canDelete)
//        : this(Guid.NewGuid().ToString(), name, rawSql, fields, canDelete)
//    {
//    }
//}
//public enum FieldDtoType
//{
//    DbField,
//    RawSqlField
//}
//public sealed record FieldDto(
//    FieldDtoType FieldType,
//    string Name,
//    string RawSql);

//public record RelationDto(
//    string FromEntityId, 
//    string ToEntityId, 
//    (string FromDbColumn, string ToDbColumn)[] RelatedDbColumns);

//public sealed record MetadataDto(
//    IReadOnlyList<EntityDto> Entities,
//    IReadOnlyList<RelationDto> Relationships);
