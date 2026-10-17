using Demo.Contracts.Repositories;
using Namh.Data.DynamicSql;

namespace Demo.Contracts.Services;

public interface IQueryRequestService
{
    IReadOnlyList<StoredQueryRequest> ListQueryRequests();
    StoredQueryRequest SaveQueryRequest(QueryDefinition query, string? fileName = null, string? requestText = null);
    StoredQueryRequest? LoadQueryRequest(string fileName);
    string GetQueryRequestString(QueryDefinition query);
    QueryDefinition ParseQueryRequestString(string text, string entity);
    QueryDefinition ParseQueryRequestString(string text);
    StoredQueryRequest? UpdateQueryRequest(string fileName, QueryDefinition query, string? requestText = null);
    bool DeleteQueryRequest(string fileName);
}
