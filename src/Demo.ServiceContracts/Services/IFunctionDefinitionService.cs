using Demo.ServiceContracts.Dto;
using Namh.Data.DynamicSql;

namespace Demo.ServiceContracts.Services;

public interface IFunctionDefinitionService
{
    IReadOnlyList<FunctionDefinitionDto> GetFunctionDefinitions();
    IReadOnlyList<SqlFunctionDefintion> GetCustomFunctionDefinitions();
    FunctionDefinitionDto CreateFunctionDefinition(FunctionDefinitionDto dto);
    FunctionDefinitionDto UpdateFunctionDefinition(string functionName, FunctionDefinitionDto dto);
    void DeleteFunctionDefinition(string functionName);
}
