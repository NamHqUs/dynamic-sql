using Demo.Contracts.Dto;
using Demo.Contracts.Repositories;
using Demo.Contracts.Services;
using Namh.Data.DynamicSql;

namespace Demo.Services;

public sealed class FunctionDefinitionService(IFunctionDefinitionRepository repository) : IFunctionDefinitionService
{
    public IReadOnlyList<FunctionDefinitionDto> GetAll()
    {
        var userDefinedNames = repository.GetUserDefined()
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return repository.GetAll()
            .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(definition =>
            {
                var dto = FunctionDefinitionDto.FromFunctionDefinition(definition);
                dto.CanDelete = userDefinedNames.Contains(definition.Name);
                dto.IsBuiltin = !dto.CanDelete;
                return dto;
            })
            .ToArray();
    }

    public FunctionDefinitionDto Create(FunctionDefinitionDto dto)
    {
        var definition = BuildDefinition(dto);
        if (repository.GetAll().Any(existing =>
            string.Equals(existing.Name, definition.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A function named '{definition.Name}' already exists.");

        repository.Save(definition);
        var response = FunctionDefinitionDto.FromFunctionDefinition(definition);
        response.CanDelete = true;
        response.IsBuiltin = false;
        return response;
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
        var userDefinitions = repository.GetUserDefined();
        var existing = userDefinitions.FirstOrDefault(item =>
            string.Equals(item.Name, functionName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            if (repository.GetAll().Any(item =>
                string.Equals(item.Name, functionName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Built-in function '{functionName}' cannot be changed.");
            throw new KeyNotFoundException($"Function '{functionName}' was not found.");
        }
        if (repository.GetAll().Any(item =>
            !string.Equals(item.Name, functionName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Name, definition.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A function named '{definition.Name}' already exists.");

        repository.Save(definition);
        if (!string.Equals(existing.Name, definition.Name, StringComparison.OrdinalIgnoreCase))
            repository.Delete(existing.Name);
        var response = FunctionDefinitionDto.FromFunctionDefinition(definition);
        response.CanDelete = true;
        response.IsBuiltin = false;
        return response;
    }

    public void Delete(string functionName)
    {
        if (!repository.GetUserDefined().Any(item =>
            string.Equals(item.Name, functionName, StringComparison.OrdinalIgnoreCase)))
        {
            if (repository.GetAll().Any(item =>
                string.Equals(item.Name, functionName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Built-in function '{functionName}' cannot be deleted.");
            return;
        }

        repository.Delete(functionName);
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
