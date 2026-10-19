using Demo.Contracts.Dto;
using Namh.Data.DynamicSql;

namespace Demo.Contracts.Services;

public interface IQueryRequestService
{
    IReadOnlyList<SavedQueryRequestDto> List();
    void Save(string name, QueryDefinition query);
    bool IsBuiltin(string name);
    bool Delete(string name);
}
