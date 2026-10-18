using Demo.Contracts.Dto;
using Namh.Data.DynamicSql;

namespace Demo.Contracts.Services;

public interface IMetadataService
{
    IReadOnlyList<MetadataEntityDto> GetAll();
    bool IsBuiltin(string entityName);
    Entity Create(Entity entity);
    Entity Update(string entityName, Entity entity);
    void Delete(string entityName);
    IReadOnlyList<Entity> Reset();
    IMetaProvider GetMetaProvider();
}
