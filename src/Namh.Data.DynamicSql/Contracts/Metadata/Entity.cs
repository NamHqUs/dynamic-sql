using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130
public interface IEntity
{
    string Name { get; }
    string? DbSchema { get; }
    string? DbTable { get; }
    List<IField> Fields { get; }
    List<Relation>? Relations { get; }

    IField GetField(string fieldName);
    Relation GetRelation(string relationName);
}

public record Entity : IEntity
{
    [JsonConstructor]
    public Entity(
        string Name,
        List<IField> Fields,
        string? DbSchema = null,
        string? DbTable = null,
        List<Relation>? Relations = null)
    {
        this.Name = string.IsNullOrWhiteSpace(Name)
            ? throw new ArgumentException("Entity name is required.", nameof(Name))
            : Name;
        this.Fields = Fields ?? throw new ArgumentNullException(nameof(Fields));
        this.DbSchema = DbSchema;
        this.DbTable = string.IsNullOrWhiteSpace(DbTable) ? Name : DbTable;
        this.Relations = Relations;
    }

    public Entity(
        string Name,
        string DbSchema,
        string DbTable,
        List<IField> Fields,
        List<Relation>? Relations = null)
        : this(Name, Fields, DbSchema, DbTable, Relations)
    {
    }

    public string Name { get; init; }
    public string? DbSchema { get; init; }
    public string DbTable { get; init; }
    public List<IField> Fields { get; init; }
    public List<Relation>? Relations { get; init; }

    public IField GetField(string fieldName)
        => Fields.FirstOrDefault(e => string.Equals(e.Name, fieldName, StringComparison.OrdinalIgnoreCase))
            ?? throw new FieldNotFoundException(Name, fieldName);

    public Relation GetRelation(string relationName)
        => Relations?.FirstOrDefault(e => string.Equals(e.Name, relationName, StringComparison.OrdinalIgnoreCase))
            ?? throw new RelationNotFoundException(Name, relationName);

    public override string ToString()
        => $"{Name}:{DbSchema}.{DbTable}";
}

public record RawSqlEntity : Entity
{
    [JsonConstructor]
    public RawSqlEntity(
        string Name,
        string RawSql,
        List<IField> Fields,
        List<Relation>? Relations = null)
        : base(Name, Fields, Relations: Relations)
    {
        this.RawSql = string.IsNullOrWhiteSpace(RawSql)
            ? throw new ArgumentException("Raw SQL is required.", nameof(RawSql))
            : RawSql;
    }

    public string RawSql { get; init; }

    public override string ToString()
        => $"{Name}:{RawSql}";
}
