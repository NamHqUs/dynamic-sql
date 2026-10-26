using Demo.Contracts.Dto;
using Namh.Data.DynamicSql;

namespace Demo.Contracts.Services;

public interface IQueryRequestService
{
    IReadOnlyList<QueryDto> List();
    void Save(string id, QueryDto query);
    bool IsBuiltin(string name);
    bool Delete(string name);
}
