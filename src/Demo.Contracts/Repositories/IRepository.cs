namespace Demo.Contracts.Repositories;

public interface IRepository<T>
{
    IReadOnlyCollection<T> GetBuiltin();
    IReadOnlyCollection<T> GetUserDefined(string userSectionId);
    T? Get(string userSectionId, string name);
    void Save(string userSectionId, T item);
    bool Delete(string userSectionId, string name);
}
