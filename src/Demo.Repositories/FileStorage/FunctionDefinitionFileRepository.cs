using Demo.Contracts.Dto;
using Demo.Contracts.Repositories;
using Demo.Share;
using Namh.Data.DynamicSql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Demo.Repositories.FileStorage;

public sealed class FunctionDefinitionFileRepository(FilePathResolver filePathResolver)
    : RepositoryBase<SqlFunctionDefintion>, IFunctionDefinitionRepository
{
    private const string FOLDER_NAME = "FunctionDefinitions";

    public override IReadOnlyCollection<SqlFunctionDefintion> GetBuiltin() => GetBuiltInDefinitions();

    public override IReadOnlyCollection<SqlFunctionDefintion> GetUserDefined(string userSectionId)
    {
        var dir = filePathResolver.GetUserStoreFolder(userSectionId, FOLDER_NAME);
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

    public override SqlFunctionDefintion? Get(string userSectionId, string name)
        => GetUserDefined(userSectionId).FirstOrDefault(definition =>
            string.Equals(definition.Name, name, StringComparison.OrdinalIgnoreCase));

    public override void Save(string userSectionId, SqlFunctionDefintion definition)
    {
        Directory.CreateDirectory(filePathResolver.GetUserStoreFolder(userSectionId, FOLDER_NAME));
        var path = GetPath(userSectionId, definition.Name);
        File.WriteAllText(path, JsonSerializer.Serialize(
            FunctionDefinitionDto.FromFunctionDefinition(definition),
            new JsonSerializerOptions { WriteIndented = true }));
    }

    public override bool Delete(string userSectionId, string functionName)
    {
        var path = GetPath(userSectionId, functionName);
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    private static IReadOnlyCollection<SqlFunctionDefintion> GetBuiltInDefinitions()
        => new MetaProvider([])
            .RegisterFunctions(CustomFunctionDefintions.Functions)
            .GetFunctions();

    private string GetPath(string userSectionId, string name)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.ToUpperInvariant())));
        var dir = filePathResolver.GetUserStoreFolder(userSectionId, FOLDER_NAME);
        return Path.Combine(dir, $"{hash}.json");
    }
}
