using Namh.Data.DynamicSql;

namespace Demo.Contracts.Repositories;

public interface IMetadataRepository
{
    IReadOnlyList<Entity> Reset();
    IReadOnlyList<Entity> GetAll();
    IReadOnlyList<Entity> GetUserDefined();
    void Save(Entity entity);
    void Delete(string entityName);
}
