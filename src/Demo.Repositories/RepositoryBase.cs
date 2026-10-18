using Demo.Contracts.Repositories;

namespace Demo.Repositories;

public abstract class RepositoryBase<T> : IRepository<T>
{
    public abstract IReadOnlyCollection<T> GetBuiltin();
    public abstract IReadOnlyCollection<T> GetUserDefined(string userSectionId);
    public abstract T? Get(string userSectionId, string name);
    public abstract void Save(string userSectionId, T item);
    public abstract bool Delete(string userSectionId, string name);
}
