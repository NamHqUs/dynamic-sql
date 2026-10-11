#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public record Entity
{
    public Entity(
        string? Name,
        List<Field> Fields,
        string? RawSql = null,
        List<Relation>? Relations = null)
    {
        if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(RawSql))
            throw new ArgumentException("Either entity name or physical table name must be provided.");

        this.Name = string.IsNullOrWhiteSpace(Name) ? RawSql! : Name;
        this.RawSql = string.IsNullOrWhiteSpace(RawSql) ? Name! : RawSql;
        this.Fields = Fields ?? throw new ArgumentNullException(nameof(Fields));
        this.Relations = Relations;
    }

    public string Name { get; init; }
    public List<Field> Fields { get; init; }
    public string RawSql { get; init; }
    public List<Relation>? Relations { get; init; }

    public Field GetField(string fieldName)
        => Fields?.FirstOrDefault(e => string.Equals(e.Name, fieldName, StringComparison.OrdinalIgnoreCase))
            ?? throw new FieldNotFoundException(Name, fieldName);

    public Relation GetRelation(string relationName)
        => Relations?.FirstOrDefault(e => string.Equals(e.Name, relationName, StringComparison.OrdinalIgnoreCase))
            ?? throw new RelationNotFoundException(Name, relationName);

    public override string ToString()
        => string.Equals(RawSql, Name, StringComparison.Ordinal) ? $"dbo.[{Name}]" : RawSql;
}
