using Namh.Data.DynamicSql;

namespace Demo.ServiceContracts.Repositories;

/// <summary>Represents a saved query definition and its original text, if available.</summary>
public sealed record StoredQueryRequest(string FileName, QueryDefinition Query, string? RequestText = null);

public interface IQueryRequestRepository
{
    IReadOnlyList<StoredQueryRequest> GetAll();
    StoredQueryRequest? Get(string fileName);
    void Save(StoredQueryRequest request);
    bool Delete(string fileName);
}
