using Namh.Data.DynamicSql;

namespace Demo.ServiceContracts.Repositories;

public interface IMetadataRepository
{
    IReadOnlyList<Entity> Reset();
    IReadOnlyList<Entity> GetAll();
    void Save(Entity entity);
    void Delete(string entityName);
}
