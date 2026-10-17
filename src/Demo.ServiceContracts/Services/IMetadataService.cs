using Namh.Data.DynamicSql;

namespace Demo.ServiceContracts.Services;

public interface IMetadataService
{
    IReadOnlyList<Entity> GetMetadata();
    Entity CreateMetadata(Entity entity);
    Entity UpdateMetadata(string entityName, Entity entity);
    void DeleteMetadata(string entityName);
    IReadOnlyList<Entity> Reset();
    IMetaProvider GetMetaProvider();
}
