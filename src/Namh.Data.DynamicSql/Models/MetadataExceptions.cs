#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130
public abstract class MetadataNotFoundException : Exception
{
    public MetadataNotFoundException(string message) : base(message) { }
}

public class EntityNotFoundException : MetadataNotFoundException
{
    public string EntityName { get; }

    public EntityNotFoundException(string entityName)
        : base($"Entity [{entityName}] was not found.")
    {
        EntityName = entityName;
    }
}

public class FieldNotFoundException : MetadataNotFoundException
{
    public string EntityName { get; }
    public string FieldName { get; }

    public FieldNotFoundException(string entityName, string fieldName)
        : base($"Field [{fieldName}] was not found in entity [{entityName}].")
    {
        EntityName = entityName;
        FieldName = fieldName;
    }
}

public class RelationNotFoundException : MetadataNotFoundException
{
    public string EntityName { get; }
    public string RelationName { get; }

    public RelationNotFoundException(string entityName, string relationName)
        : base($"Relation [{relationName}] was not found in entity [{entityName}].")
    {
        EntityName = entityName;
        RelationName = relationName;
    }
}
