using Namh.Data.DynamicSql;

namespace Demo.ServiceContracts.Services;

public interface IMetadataService
{
    IReadOnlyList<Entity> GetAll();
    Entity Create(Entity entity);
    Entity Update(string entityName, Entity entity);
    void Delete(string entityName);
    IReadOnlyList<Entity> Reset();
    IMetaProvider GetMetaProvider();
}
