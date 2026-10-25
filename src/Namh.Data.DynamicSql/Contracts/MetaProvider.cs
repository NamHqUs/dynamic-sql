#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public interface IMetaProvider
{
    Entity GetEntity(string entityName);
}

public class MetaProvider : IMetaProvider
{
    private readonly Dictionary<string, Entity> _entities;

    public MetaProvider(IEnumerable<Entity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        _entities = entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
    }

    public Entity GetEntity(string entityName)
    {
        if (_entities.TryGetValue(entityName, out Entity? entity))
            return entity;

        throw new EntityNotFoundException(entityName);
    }
}
