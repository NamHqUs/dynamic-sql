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
        var merge = BuiltinMetadata.DefaultMetadata.ToDictionary(
            entity => entity.Name,
            StringComparer.OrdinalIgnoreCase);
        foreach (var entity in GetUserDefined())
        {
            if (!merge.TryAdd(entity.Name, entity))
                throw new InvalidDataException($"User metadata cannot replace built-in entity '{entity.Name}'.");
        }
        return merge.Values.ToArray();
    }

    public IReadOnlyList<Entity> GetUserDefined()
    {
        var dir = filePathResolver.GetUserStoreFolder(FOLDER_NAME);
        if (!Directory.Exists(dir))
            return [];

        return Directory.EnumerateFiles(dir, "*.json")
            .Select(file => JsonSerializer.Deserialize<Entity>(File.ReadAllText(file))
                ?? throw new InvalidOperationException($"Failed to deserialize metadata from '{file}'."))
            .ToArray();
    }

    public bool IsBuiltin(string entityName)
        => BuiltinMetadata.DefaultMetadata.Any(entity =>
            string.Equals(entity.Name, entityName, StringComparison.OrdinalIgnoreCase));

    public void Save(Entity entity)
    {
        if (BuiltinMetadata.DefaultMetadata.Any(builtin =>
            string.Equals(builtin.Name, entity.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Built-in entity '{entity.Name}' cannot be changed.");

        var dir = filePathResolver.GetUserStoreFolder(FOLDER_NAME);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            GetPath(entity.Name),
            JsonSerializer.Serialize(entity, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Delete(string entityName)
    {
        if (BuiltinMetadata.DefaultMetadata.Any(builtin =>
            string.Equals(builtin.Name, entityName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Built-in entity '{entityName}' cannot be deleted.");

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
