using Namh.Data.DynamicSql;

namespace Demo.Contracts.Repositories;

/// <summary>Represents a saved query definition and its original text, if available.</summary>
public sealed record StoredQueryRequest(
    string FileName,
    QueryDefinition Query,
    string? RequestText = null,
    string? Description = null,
    string? ChartType = null)
{
    public bool CanDelete { get; init; } = true;
}

public sealed record BuiltinQueryRequestFile(string FileName, string RequestText);

public interface IQueryRequestRepository
{
    IReadOnlyList<StoredQueryRequest> GetAllCustom();
    IReadOnlyList<BuiltinQueryRequestFile> GetBuiltinFiles();
    StoredQueryRequest? Get(string fileName);
    void Save(StoredQueryRequest request);
    bool Delete(string fileName);
}
