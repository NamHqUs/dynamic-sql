namespace Namh.Data.DynamicSql.Model;
public enum RelationType
{
    Lookup = 0,
    Collection
}

public record RelationItem(string FromDbColumn, string ToDbColumn);
public record Relation(string Name, string RelatedEntity, RelationType Type, List<RelationItem> RelatedKeys);
