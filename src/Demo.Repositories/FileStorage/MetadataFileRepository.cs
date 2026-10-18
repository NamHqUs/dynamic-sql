using Demo.Contracts.Repositories;
using Demo.Share;
using Namh.Data.DynamicSql;
using System.Text.Json;

namespace Demo.Repositories.FileStorage;

public sealed class MetadataFileRepository(FilePathResolver filePathResolver) : IMetadataRepository
{
    private const string FOLDER_NAME = "Metadata";

    public IReadOnlyList<Entity> Reset()
    {
        var dir = filePathResolver.GetUserStoreFolder(FOLDER_NAME);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
        return GetAll();
    }

    public IReadOnlyList<Entity> GetAll()
    {
        var dir = filePathResolver.GetUserStoreFolder(FOLDER_NAME);
        if (!Directory.Exists(dir))
            return BuiltinMetadata.DefaultMetadata;

        var customeEntities = Directory.EnumerateFiles(dir, "*.json")
            .Select(file => JsonSerializer.Deserialize<Entity>(File.ReadAllText(file))
                ?? throw new InvalidOperationException($"Failed to deserialize metadata from '{file}'."))
            .ToArray();

        var merge = BuiltinMetadata.DefaultMetadata.ToDictionary(e => e.Name);
        foreach (var entity in customeEntities)
        {
            if (IsBuiltin(entity.Name))
                throw new InvalidDataException($"Custom metadata cannot replace built-in entity '{entity.Name}'.");
            merge[entity.Name] = entity;
        }
        return merge.Values.ToArray();
    }

    public bool IsBuiltin(string entityName)
        => BuiltinMetadata.DefaultMetadata.Any(entity =>
            string.Equals(entity.Name, entityName, StringComparison.OrdinalIgnoreCase));

    public void Save(Entity entity)
    {
        var dir = filePathResolver.GetUserStoreFolder(FOLDER_NAME);
        Directory.CreateDirectory(dir);
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
        var dir = filePathResolver.GetUserStoreFolder(FOLDER_NAME);
        var path = Path.GetFullPath(Path.Combine(dir, $"{name}.json"));
        var root = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Entity name is not a valid file name.", nameof(name));
        return path;
    }
}
