using Demo.Contracts.Dto;
using Namh.Data.DynamicSql;

namespace Demo.Contracts.Services;

public interface IQueryRequestService
{
    IReadOnlyList<QueryRequestDto> List();
    void Save(string name, QueryRequestDto queryRequest);
    bool IsBuiltin(string name);
    bool Delete(string name);
}
