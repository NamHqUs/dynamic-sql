using System.Text.Json;
using Demo.ServiceContracts.Repositories;
using Namh.Data.DynamicSql;

namespace Demo.Repositories.FileStorage;

public sealed class MetadataFileRepository : IMetadataRepository
{
    private readonly string _directory = Path.Combine(Environment.CurrentDirectory, "DataStore", "Metadata");

    public IReadOnlyList<Entity> GetAll()
    {
        if (!Directory.Exists(_directory))
            return [];

        return Directory.EnumerateFiles(_directory, "*.json")
            .Select(file => JsonSerializer.Deserialize<Entity>(File.ReadAllText(file))
                ?? throw new InvalidOperationException($"Failed to deserialize metadata from '{file}'."))
            .ToArray();
    }

    public void Save(Entity entity)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            GetPath(entity.Name),
            JsonSerializer.Serialize(entity, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Delete(string entityName)
    {
        var path = GetPath(entityName);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string GetPath(string name)
    {
        var path = Path.GetFullPath(Path.Combine(_directory, $"{name}.json"));
        var root = Path.GetFullPath(_directory) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Entity name is not a valid file name.", nameof(name));
        return path;
    }
}
