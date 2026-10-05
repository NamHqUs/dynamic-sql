using Namh.Data.DynamicSql.Model;

namespace Namh.Data.DynamicSql
{
    public interface IMetaProvider
    {
        Entity Get(string entityName);
    }

    public class MetaProvider(List<Entity> entities) : IMetaProvider
    {
        private readonly Dictionary<string, Entity> _entities = entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        public Entity Get(string entityName)
        {
            if (_entities.TryGetValue(entityName, out Entity? entity))
                return entity;

            throw new Exception($"Entity {entityName} is not registerd in {typeof(MetaProvider).Name}");
        }
    }
}
