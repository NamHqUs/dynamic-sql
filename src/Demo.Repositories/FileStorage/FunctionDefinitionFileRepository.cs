using Demo.ServiceContracts.Dto;
using Demo.ServiceContracts.Repositories;
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
            .RegisterFunctions(GetCustomDefinitions())
            .GetFunctions();


    public IReadOnlyCollection<SqlFunctionDefintion> GetCustomDefinitions()
    {
        var definitions = CustomFunctionDefintions.Functions.ToDictionary(
            definition => definition.Name,
            StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(filePathResolver.GetUserStoreFolder(FOLDER_NAME)))
        {
            foreach (var path in Directory.EnumerateFiles(filePathResolver.GetUserStoreFolder(FOLDER_NAME), "*.json"))
            {
                var dto = JsonSerializer.Deserialize<FunctionDefinitionDto>(File.ReadAllText(path))
                    ?? throw new InvalidOperationException($"Failed to deserialize function definition from '{path}'.");
                var definition = dto.ToFunctionDefinition();
                definitions[definition.Name] = definition;
            }
        }
        return definitions.Values.ToArray();
    }

    public void Save(SqlFunctionDefintion definition)
    {
        var path = GetPath(definition.Name);
        File.WriteAllText(path, JsonSerializer.Serialize(
            FunctionDefinitionDto.FromFunctionDefinition(definition),
            new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Delete(string functionName)
    {
        var path = GetPath(functionName);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string GetPath(string name)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.ToUpperInvariant())));
        var dir = filePathResolver.GetUserStoreFolder(FOLDER_NAME);
        return Path.Combine(dir, $"{hash}.json");
    }
}
