using System.Security.Cryptography;
using System.Text;
using Demo.Share;
using Demo.ServiceContracts.Dto;
using Demo.ServiceContracts.Repositories;
using Namh.Data.DynamicSql;
using System.Text.Json;

namespace Demo.Repositories.FileStorage;

public sealed class FunctionDefinitionFileRepository : IFunctionDefinitionRepository
{
    private readonly string _directory = Path.Combine(Environment.CurrentDirectory, "DataStore", "FunctionDefinitions");

    public IReadOnlyList<SqlFunctionDefintion> GetBuiltInDefinitions()
        => new MetaProvider([]).GetFunctions().ToArray();

    public IReadOnlyList<SqlFunctionDefintion> GetCustomDefinitions()
    {
        var definitions = CustomFunctionDefintions.Functions.ToDictionary(
            definition => definition.Name,
            StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(_directory))
        {
            foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
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
        Directory.CreateDirectory(_directory);
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
        return Path.Combine(_directory, $"{hash}.json");
    }
}
