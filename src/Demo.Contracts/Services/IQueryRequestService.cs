using Demo.Contracts.Dto;
using Namh.Data.DynamicSql;

namespace Demo.Contracts.Services;

public interface IQueryRequestService
{
    IReadOnlyList<SavedQueryRequestDto> List();
    void Save(string name, SavedQueryRequestDto queryRequest);
    bool IsBuiltin(string name);
    bool Delete(string name);
}
