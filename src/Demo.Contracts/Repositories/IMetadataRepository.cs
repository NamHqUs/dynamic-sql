using Namh.Data.DynamicSql;

namespace Demo.Contracts.Repositories;

public interface IMetadataRepository : IRepository<Entity>
{
    void Reset(string userSectionId);
}
