using Demo.Contracts.Dto;
using Demo.Contracts.Repositories;
using Demo.Contracts.Services;
using Namh.Data.DynamicSql;

namespace Demo.Services;

public sealed class FunctionDefinitionService(IFunctionDefinitionRepository repository) : IFunctionDefinitionService
{
    public IReadOnlyList<FunctionDefinitionDto> GetAll() => repository.GetAll()
        .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
        .Select(ToDto)
        .ToArray();

    public FunctionDefinitionDto Create(FunctionDefinitionDto dto)
    {
        var definition = BuildDefinition(dto);
        if (repository.GetAll().Any(existing =>
            string.Equals(existing.Name, definition.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A function named '{definition.Name}' already exists.");

        repository.Save(definition);
        return ToDto(definition);
    }

    public FunctionDefinitionDto Update(string functionName, FunctionDefinitionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (repository.IsBuiltin(functionName))
            throw new InvalidOperationException($"Built-in function '{functionName}' is read-only.");
        if (!repository.GetAll().Any(existing =>
            string.Equals(existing.Name, functionName, StringComparison.OrdinalIgnoreCase)))
            throw new KeyNotFoundException($"Function '{functionName}' was not found.");
        if (!string.Equals(functionName, dto.Name, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A function name cannot be changed.", nameof(dto));

        var definition = BuildDefinition(dto);
        repository.Save(definition);
        return ToDto(definition);
    }

    public void Delete(string functionName)
    {
        if (repository.IsBuiltin(functionName))
            throw new InvalidOperationException($"Built-in function '{functionName}' is read-only.");

        repository.Delete(functionName);
    }

    private FunctionDefinitionDto ToDto(SqlFunctionDefintion definition)
    {
        var isBuiltin = repository.IsBuiltin(definition.Name);
        var dto = FunctionDefinitionDto.FromFunctionDefinition(definition);
        dto.IsBuiltin = isBuiltin;
        dto.CanDelete = !isBuiltin;
        return dto;
    }

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
