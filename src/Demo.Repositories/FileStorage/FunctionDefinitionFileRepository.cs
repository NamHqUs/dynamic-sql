using Demo.Contracts.Dto;
using Demo.Contracts.Repositories;
using Demo.Share;
using Namh.Data.DynamicSql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Demo.Repositories.FileStorage;

public sealed class FunctionDefinitionFileRepository(FilePathResolver filePathResolver) : IFunctionDefinitionRepository
{
    private const string FOLDER_NAME = "FunctionDefinitions";

    public IReadOnlyCollection<SqlFunctionDefintion> GetAll()
        => new MetaProvider([])
            .RegisterFunctions(CustomFunctionDefintions.Functions)
            .RegisterFunctions(GetUserDefined())
            .GetFunctions();

    public IReadOnlyCollection<SqlFunctionDefintion> GetUserDefined()
    {
        var dir = filePathResolver.GetUserStoreFolder(FOLDER_NAME);
        if (!Directory.Exists(dir))
            return [];

        var builtInNames = GetBuiltInDefinitions()
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var definitions = new Dictionary<string, SqlFunctionDefintion>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            var dto = JsonSerializer.Deserialize<FunctionDefinitionDto>(File.ReadAllText(path))
                ?? throw new InvalidOperationException($"Failed to deserialize function definition from '{path}'.");
            var definition = dto.ToFunctionDefinition();
            if (builtInNames.Contains(definition.Name))
                throw new InvalidDataException($"User function '{definition.Name}' cannot replace a built-in function.");
            if (!definitions.TryAdd(definition.Name, definition))
                throw new InvalidDataException($"Duplicate user function '{definition.Name}' found in '{path}'.");
        }
        return definitions.Values.ToArray();
    }

    public void Save(SqlFunctionDefintion definition)
    {
        if (GetBuiltInDefinitions().Any(builtin =>
            string.Equals(builtin.Name, definition.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Built-in function '{definition.Name}' cannot be changed.");

        Directory.CreateDirectory(filePathResolver.GetUserStoreFolder(FOLDER_NAME));
        var path = GetPath(definition.Name);
        File.WriteAllText(path, JsonSerializer.Serialize(
            FunctionDefinitionDto.FromFunctionDefinition(definition),
            new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Delete(string functionName)
    {
        if (GetBuiltInDefinitions().Any(builtin =>
            string.Equals(builtin.Name, functionName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Built-in function '{functionName}' cannot be deleted.");

        var path = GetPath(functionName);
        if (File.Exists(path))
            File.Delete(path);
    }

    private static IReadOnlyCollection<SqlFunctionDefintion> GetBuiltInDefinitions()
        => new MetaProvider([])
            .RegisterFunctions(CustomFunctionDefintions.Functions)
            .GetFunctions();

    private string GetPath(string name)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.ToUpperInvariant())));
        var dir = filePathResolver.GetUserStoreFolder(FOLDER_NAME);
        return Path.Combine(dir, $"{hash}.json");
    }
}
