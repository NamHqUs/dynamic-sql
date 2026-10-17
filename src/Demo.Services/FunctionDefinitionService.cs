using Demo.ServiceContracts.Dto;
using Demo.ServiceContracts.Repositories;
using Demo.ServiceContracts.Services;
using Namh.Data.DynamicSql;

namespace Demo.Services;

public sealed class FunctionDefinitionService(IFunctionDefinitionRepository repository) : IFunctionDefinitionService
{
    public IReadOnlyList<FunctionDefinitionDto> GetAll() => repository.GetAll()
        .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
        .Select(FunctionDefinitionDto.FromFunctionDefinition)
        .ToArray();

    public FunctionDefinitionDto Create(FunctionDefinitionDto dto)
    {
        var definition = BuildDefinition(dto);
        repository.Save(definition);
        var response = FunctionDefinitionDto.FromFunctionDefinition(definition);
        response.CanDelete = true;
        return response;
    }

    public FunctionDefinitionDto Update(string functionName, FunctionDefinitionDto dto)
    {
        var definition = BuildDefinition(dto);
        repository.Save(definition);
        var response = FunctionDefinitionDto.FromFunctionDefinition(definition);
        response.CanDelete = true;
        return response;
    }

    public void Delete(string functionName) => repository.Delete(functionName);

    private static SqlFunctionDefintion BuildDefinition(FunctionDefinitionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Function name is required.");
        if (dto.Arguments is null)
            throw new ArgumentException("Function arguments are required.");
        if (dto.SqlTemplates is null)
            throw new ArgumentException("SQL templates are required.");
        if (dto.Arguments.Any(argument => argument is null))
            throw new ArgumentException("Function arguments cannot contain null definitions.");

        var definition = dto.ToFunctionDefinition();
        _ = new MetaProvider([], [definition]);
        return definition;
    }
}
