using Demo.Contracts.Repositories;
using Demo.Contracts.Dto;
using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.Contracts.QueryDefinitions;
using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace Demo.Repositories.FileStorage;

public sealed class QueryRequestFileRepository(FilePathResolver filePathResolver)
    : FileRepositoryBase<SavedQueryRequestDto>(filePathResolver, FOLDER_NAME, FILE_EXTENSION),
        IQueryRequestRepository
{
    private const string FOLDER_NAME = "QueryRequests";
    private const string FILE_EXTENSION = ".yml";

    public override IReadOnlyCollection<SavedQueryRequestDto> GetBuiltin()
    {
        var dir = GetBuiltinStoreFolder();
        Directory.CreateDirectory(dir);
        return Directory.EnumerateFiles(dir)
            .Where(path => HasExtension(path, FILE_EXTENSION))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => ReadQueryRequest(path, isBuiltin: true))
            .ToArray();
    }

    public override IReadOnlyCollection<SavedQueryRequestDto> GetUserDefined(string userSectionId)
    {
        var dir = GetUserStoreFolder(userSectionId);
        if (!Directory.Exists(dir))
            return [];

        return Directory.EnumerateFiles(dir)
            .Where(path => HasExtension(path, FILE_EXTENSION))
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => ReadQueryRequest(path, isBuiltin: false))
            .ToArray();
    }

    public override SavedQueryRequestDto? Get(string userSectionId, string id)
    {
        var path = GetUserFilePath(userSectionId, id);
        return File.Exists(path)
            ? ReadQueryRequest(path, isBuiltin: false)
            : null;
    }

    public override void Save(string userSectionId, SavedQueryRequestDto queryRequest)
    {
        ArgumentNullException.ThrowIfNull(queryRequest);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryRequest.Name);
        ArgumentNullException.ThrowIfNull(queryRequest.Query);
        ArgumentNullException.ThrowIfNull(queryRequest.ProjectionRows);

        Directory.CreateDirectory(GetUserStoreFolder(userSectionId));
        File.WriteAllText(
            GetUserFilePath(userSectionId, queryRequest.Name),
            Serialize(queryRequest));
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

    private static SavedQueryRequestDto ReadQueryRequest(string path, bool isBuiltin)
    {
        var definition = new DeserializerBuilder()
            .Build()
            .Deserialize<YamlQueryDefinition>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Query request file '{path}' is empty.");
        if (string.IsNullOrWhiteSpace(definition.Name))
            throw new InvalidDataException($"Query request file '{path}' is missing its name.");

        return new SavedQueryRequestDto(
            definition.Name,
            QueryDefinitionParser.Parse(definition),
            definition.ProjectionRows ?? [],
            isBuiltin);
    }

    private static string Serialize(SavedQueryRequestDto queryRequest)
    {
        var lines = new List<string>
        {
            $"Name: {JsonSerializer.Serialize(queryRequest.Name)}"
        };
        if (queryRequest.ProjectionRows.Count > 0)
            lines.Add($"ProjectionRows: {JsonSerializer.Serialize(queryRequest.ProjectionRows)}");
        lines.Add(queryRequest.Query.ToYml());
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
