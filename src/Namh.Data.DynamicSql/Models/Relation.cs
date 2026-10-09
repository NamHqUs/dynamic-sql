#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130
public enum RelationType
{
    Lookup = 0,
    Collection
}

public record RelationItem(string FromDbColumn, string ToDbColumn);

public record Relation(
    string Name,
    string RelatedEntity,
    RelationType Type,
    List<RelationItem> RelatedKeys)
{
}
