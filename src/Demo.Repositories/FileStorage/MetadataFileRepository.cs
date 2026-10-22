using Demo.Contracts.Repositories;
using Demo.Share;
using Namh.Data.DynamicSql;
using System.Text.Json;

namespace Demo.Repositories.FileStorage;

public sealed class MetadataFileRepository(FilePathResolver filePathResolver)
    : FileRepositoryBase<Entity>(filePathResolver, FOLDER_NAME), IMetadataRepository
{
    private const string FOLDER_NAME = "UnitTestData";

    public void Reset(string userSectionId)
    {
        var dir = GetUserStoreFolder(userSectionId);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    public override IReadOnlyCollection<Entity> GetBuiltin() => BuiltinMetadata.DefaultMetadata;

    public override IReadOnlyCollection<Entity> GetUserDefined(string userSectionId)
    {
        var dir = GetUserStoreFolder(userSectionId);
        if (!Directory.Exists(dir))
            return [];

        return Directory.EnumerateFiles(dir, "*.json")
            .Select(file => JsonSerializer.Deserialize<Entity>(File.ReadAllText(file))
                ?? throw new InvalidOperationException($"Failed to deserialize metadata from '{file}'."))
            .ToArray();
    }

    public override Entity? Get(string userSectionId, string id)
        => GetUserDefined(userSectionId).FirstOrDefault(entity =>
            string.Equals(entity.Name, id, StringComparison.OrdinalIgnoreCase));

    public override void Save(string userSectionId, Entity entity)
    {
        var dir = GetUserStoreFolder(userSectionId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            GetUserFilePath(userSectionId, $"{entity.Name}.json"),
            JsonSerializer.Serialize(entity, new JsonSerializerOptions { WriteIndented = true }));
    }

    public override bool Delete(string userSectionId, string id)
    {
        var path = GetUserFilePath(userSectionId, $"{id}.json");
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

}
