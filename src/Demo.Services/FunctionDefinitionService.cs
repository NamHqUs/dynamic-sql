using Demo.ServiceContracts.Dto;
using Demo.ServiceContracts.Repositories;
using Demo.ServiceContracts.Services;
using Namh.Data.DynamicSql;

namespace Demo.Services;

public sealed class FunctionDefinitionService : IFunctionDefinitionService
{
    private readonly IFunctionDefinitionRepository _repository;
    private readonly Dictionary<string, SqlFunctionDefintion> _customDefinitions;

    public FunctionDefinitionService(IFunctionDefinitionRepository repository)
    {
        _repository = repository;
        _customDefinitions = repository.GetCustomDefinitions().ToDictionary(
            definition => definition.Name,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<FunctionDefinitionDto> GetFunctionDefinitions()
    {
        return new MetaProvider([], _customDefinitions.Values)
            .GetFunctions()
            .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(definition =>
            {
                var dto = FunctionDefinitionDto.FromFunctionDefinition(definition);
                dto.CanDelete = _customDefinitions.Values.Any(item =>
                    string.Equals(item.Name, definition.Name, StringComparison.OrdinalIgnoreCase));
                return dto;
            })
            .ToArray();
    }

    public IReadOnlyList<SqlFunctionDefintion> GetCustomFunctionDefinitions()
        => _customDefinitions.Values.ToArray();

    public FunctionDefinitionDto CreateFunctionDefinition(FunctionDefinitionDto dto)
    {
        var definition = BuildDefinition(dto);
        if (GetAllDefinitions().Any(item =>
                string.Equals(item.Name, definition.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A function named '{definition.Name}' already exists.");

        _repository.Save(definition);
        _customDefinitions.Add(definition.Name, definition);
        var response = FunctionDefinitionDto.FromFunctionDefinition(definition);
        response.CanDelete = true;
        return response;
    }

    public FunctionDefinitionDto UpdateFunctionDefinition(string functionName, FunctionDefinitionDto dto)
    {
        var definition = BuildDefinition(dto);
        if (!string.Equals(functionName, definition.Name, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A function definition cannot be renamed.", nameof(dto));
        if (!GetAllDefinitions().Any(item =>
                string.Equals(item.Name, functionName, StringComparison.OrdinalIgnoreCase)))
            throw new KeyNotFoundException($"Function '{functionName}' was not found.");

        _repository.Save(definition);
        _customDefinitions[definition.Name] = definition;
        var response = FunctionDefinitionDto.FromFunctionDefinition(definition);
        response.CanDelete = true;
        return response;
    }

    public void DeleteFunctionDefinition(string functionName)
    {
        if (!_customDefinitions.TryGetValue(functionName, out var definition))
        {
            if (_repository.GetBuiltInDefinitions().Any(item =>
                    string.Equals(item.Name, functionName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Built-in function '{functionName}' cannot be deleted.");
            throw new KeyNotFoundException($"Function '{functionName}' was not found.");
        }

        _repository.Delete(functionName);
        _customDefinitions.Remove(definition.Name);
    }

    private IReadOnlyList<SqlFunctionDefintion> GetAllDefinitions()
        => new MetaProvider([], _customDefinitions.Values).GetFunctions().ToArray();

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
