using Demo.Contracts.Repositories;
using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

namespace Demo.Repositories.FileStorage;

public sealed class QueryRequestFileRepository(FilePathResolver filePathResolver)
    : FileRepositoryBase<QueryDefinition>(filePathResolver, FOLDER_NAME, FILE_EXTENSION),
        IQueryRequestRepository
{
    private const string FOLDER_NAME = "QueryRequests";
    private const string FILE_EXTENSION = ".yml";

    public override IReadOnlyCollection<QueryDefinition> GetBuiltin()
    {
        var dir = GetBuiltinStoreFolder();
        Directory.CreateDirectory(dir);
        return Directory.EnumerateFiles(dir)
            .Where(path => HasExtension(path, FILE_EXTENSION))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => QueryDefinitionParser.ParseFromYml(File.ReadAllText(path)))
            .ToArray();
    }

    public override IReadOnlyCollection<QueryDefinition> GetUserDefined(string userSectionId)
    {
        var dir = GetUserStoreFolder(userSectionId);
        if (!Directory.Exists(dir))
            return [];

        return Directory.EnumerateFiles(dir)
            .Where(path => HasExtension(path, FILE_EXTENSION))
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => QueryDefinitionParser.ParseFromYml(File.ReadAllText(path)))
            .ToArray();
    }

    public override QueryDefinition? Get(string userSectionId, string id)
    {
        var path = GetUserFilePath(userSectionId, id);
        return File.Exists(path)
            ? QueryDefinitionParser.ParseFromYml(File.ReadAllText(path))
            : null;
    }

    public override void Save(string userSectionId, QueryDefinition query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Name);

        Directory.CreateDirectory(GetUserStoreFolder(userSectionId));
        File.WriteAllText(
            GetUserFilePath(userSectionId, query.Name),
            query.ToYml() + Environment.NewLine);
    }

    public override bool Delete(string userSectionId, string id)
    {
        var path = GetUserFilePath(userSectionId, id);
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    private static bool HasExtension(string path, string extension)
        => string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);
}
