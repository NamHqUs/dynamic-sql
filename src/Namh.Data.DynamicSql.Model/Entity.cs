namespace Namh.Data.DynamicSql.Model;

public record Entity(string Name, List<Field> Fields, string? RawSql = null, List<Relation>? Relations = null)
{
    public Field GetField(string fieldName)
        => Fields?.FirstOrDefault(e => string.Equals(e.Name, fieldName, StringComparison.OrdinalIgnoreCase))
            ?? throw new FieldNotFoundException(Name, fieldName);

    public Relation GetRelation(string relationName)
        => Relations?.FirstOrDefault(e => string.Equals(e.Name, relationName, StringComparison.OrdinalIgnoreCase))
            ?? throw new RelationNotFoundException(Name, relationName);

    public override string ToString()
        => string.IsNullOrEmpty(RawSql) ? $"dbo.[{Name}]" : RawSql;
}
