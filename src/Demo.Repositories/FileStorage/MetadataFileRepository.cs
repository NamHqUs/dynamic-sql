using Demo.Contracts.Repositories;
using Demo.Share;
using Namh.Data.DynamicSql;
using System.Text.Json;

namespace Demo.Repositories.FileStorage;

public sealed class MetadataFileRepository(FilePathResolver filePathResolver)
    : RepositoryBase<Entity>, IMetadataRepository
{
    private const string FOLDER_NAME = "Metadata";

    public void Reset(string userSectionId)
    {
        var dir = filePathResolver.GetUserStoreFolder(userSectionId, FOLDER_NAME);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    public override IReadOnlyCollection<Entity> GetBuiltin() => BuiltinMetadata.DefaultMetadata;

    public override IReadOnlyCollection<Entity> GetUserDefined(string userSectionId)
    {
        var dir = filePathResolver.GetUserStoreFolder(userSectionId, FOLDER_NAME);
        if (!Directory.Exists(dir))
            return [];

        return Directory.EnumerateFiles(dir, "*.json")
            .Select(file => JsonSerializer.Deserialize<Entity>(File.ReadAllText(file))
                ?? throw new InvalidOperationException($"Failed to deserialize metadata from '{file}'."))
            .ToArray();
    }

    public override Entity? Get(string userSectionId, string name)
        => GetUserDefined(userSectionId).FirstOrDefault(entity =>
            string.Equals(entity.Name, name, StringComparison.OrdinalIgnoreCase));

    public override void Save(string userSectionId, Entity entity)
    {
        var dir = filePathResolver.GetUserStoreFolder(userSectionId, FOLDER_NAME);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            GetPath(userSectionId, entity.Name),
            JsonSerializer.Serialize(entity, new JsonSerializerOptions { WriteIndented = true }));
    }

    public override bool Delete(string userSectionId, string entityName)
    {
        var path = GetPath(userSectionId, entityName);
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    private string GetPath(string userSectionId, string name)
    {
        var dir = filePathResolver.GetUserStoreFolder(userSectionId, FOLDER_NAME);
        var path = Path.GetFullPath(Path.Combine(dir, $"{name}.json"));
        var root = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Entity name is not a valid file name.", nameof(name));
        return path;
    }
}
