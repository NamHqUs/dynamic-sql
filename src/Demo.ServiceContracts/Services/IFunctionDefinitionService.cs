using Demo.ServiceContracts.Dto;
using Namh.Data.DynamicSql;

namespace Demo.ServiceContracts.Services;

public interface IFunctionDefinitionService
{
    IReadOnlyList<FunctionDefinitionDto> GetAll();
    FunctionDefinitionDto Create(FunctionDefinitionDto dto);
    FunctionDefinitionDto Update(string functionName, FunctionDefinitionDto dto);
    void Delete(string functionName);
}
