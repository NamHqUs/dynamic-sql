using Demo.ServiceContracts.Repositories;

namespace Demo.Repositories.InMemory;

public sealed class QueryRequestMemoryRepository : IQueryRequestRepository
{
    private readonly Dictionary<string, StoredQueryRequest> _requests =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<StoredQueryRequest> GetAll()
        => _requests.Values.OrderByDescending(request => request.FileName).ToArray();

    public StoredQueryRequest? Get(string fileName)
        => _requests.GetValueOrDefault(fileName);

    public void Save(StoredQueryRequest request)
        => _requests[request.FileName] = request;

    public bool Delete(string fileName) => _requests.Remove(fileName);
}
