using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Demo.WebApi.Dto;
using Namh.Data.DynamicSql;

namespace Demo.WebApi.Services;

public static class FunctionDefinitionServices
{
    private static readonly string FunctionDefinitionsPath =
        Path.Combine(Environment.CurrentDirectory, "DataStore", "FunctionDefinitions");

    private static readonly Dictionary<string, FunctionDefinition> CustomDefinitions;

    static FunctionDefinitionServices()
    {
        var fromCodes = Demo.Share.CustomFunctionDefintions.Functions.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        var fromFile = LoadFromFile().ToDictionary(definition => definition.Name, StringComparer.OrdinalIgnoreCase);
        
        foreach (var kvp in fromFile)
            fromCodes[kvp.Key] = kvp.Value;
        CustomDefinitions = fromCodes;
    }
    private static readonly HashSet<string> BuiltInFunctionNames =
        new MetaProvider([]).GetFunctions()
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static List<FunctionDefinitionDto> GetFunctionDefinitions()
        => new MetaProvider([], CustomDefinitions.Values)
            .GetFunctions()
            .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(definition =>
            {
                var dto = FunctionDefinitionDto.FromFunctionDefinition(definition);
                dto.CanDelete = CustomDefinitions.ContainsKey(definition.Name);
                return dto;
            })
            .ToList();

    public static IReadOnlyCollection<FunctionDefinition> GetCustomFunctionDefinitions()
        => CustomDefinitions.Values.ToArray();

    public static IMetaProvider GetMetaProvider(IEnumerable<Entity> entities)
        => new MetaProvider(entities, CustomDefinitions.Values);

    public static FunctionDefinitionDto CreateFunctionDefinition(FunctionDefinitionDto dto)
    {
        var definition = BuildDefinition(dto);
        var existingNames = new MetaProvider([], CustomDefinitions.Values).GetFunctions();
        if (existingNames.Any(item => string.Equals(item.Name, definition.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A function named '{definition.Name}' already exists.");

        SaveDefinition(definition);
        CustomDefinitions.Add(definition.Name, definition);
        var response = FunctionDefinitionDto.FromFunctionDefinition(definition);
        response.CanDelete = true;
        return response;
    }

    public static FunctionDefinitionDto UpdateFunctionDefinition(string functionName, FunctionDefinitionDto dto)
    {
        var definition = BuildDefinition(dto);
        if (!string.Equals(functionName, definition.Name, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A function definition cannot be renamed.", nameof(dto));

        var existingDefinitions = new MetaProvider([], CustomDefinitions.Values).GetFunctions();
        if (!existingDefinitions.Any(item => string.Equals(item.Name, functionName, StringComparison.OrdinalIgnoreCase)))
            throw new KeyNotFoundException($"Function '{functionName}' was not found.");

        SaveDefinition(definition);
        CustomDefinitions[definition.Name] = definition;
        var response = FunctionDefinitionDto.FromFunctionDefinition(definition);
        response.CanDelete = true;
        return response;
    }

    public static void DeleteFunctionDefinition(string functionName)
    {
        if (!CustomDefinitions.TryGetValue(functionName, out var definition))
        {
            if (BuiltInFunctionNames.Contains(functionName))
                throw new InvalidOperationException($"Built-in function '{functionName}' cannot be deleted.");

            throw new KeyNotFoundException($"Function '{functionName}' was not found.");
        }

        var nameHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definition.Name.ToUpperInvariant())));
        var filePath = Path.Combine(FunctionDefinitionsPath, $"{nameHash}.json");
        File.Delete(filePath);
        CustomDefinitions.Remove(definition.Name);
    }

    private static FunctionDefinition BuildDefinition(FunctionDefinitionDto dto)
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

    private static IEnumerable<FunctionDefinition> LoadFromFile()
    {
        if (!Directory.Exists(FunctionDefinitionsPath))
            return [];

        return Directory.GetFiles(FunctionDefinitionsPath, "*.json")
            .Select(file =>
            {
                var dto = JsonSerializer.Deserialize<FunctionDefinitionDto>(File.ReadAllText(file))
                    ?? throw new InvalidOperationException($"Failed to deserialize function definition from '{file}'.");
                return BuildDefinition(dto);
            })
            .ToArray();
    }

    private static void SaveDefinition(FunctionDefinition definition)
    {
        Directory.CreateDirectory(FunctionDefinitionsPath);
        var nameHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definition.Name.ToUpperInvariant())));
        var filePath = Path.Combine(FunctionDefinitionsPath, $"{nameHash}.json");
        var json = JsonSerializer.Serialize(
            FunctionDefinitionDto.FromFunctionDefinition(definition),
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }
}
