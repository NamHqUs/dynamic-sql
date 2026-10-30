#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public interface IMetadataProvider
{
    IEntity GetEntity(string entityName);
}

public class MetadataProvider : IMetadataProvider
{
    private readonly Dictionary<string, IEntity> _entities;

    public MetadataProvider(IEnumerable<IEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        _entities = entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IEntity GetEntity(string entityName)
    {
        if (_entities.TryGetValue(entityName, out IEntity? entity))
            return entity;

        throw new EntityNotFoundException(entityName);
    }

}

public sealed class MetaProvider : MetadataProvider, IMetaProvider
{
    public MetaProvider(IEnumerable<IEntity> entities)
        : base(entities)
    {
    }
}

public interface IMetaProvider : IMetadataProvider
{
}
