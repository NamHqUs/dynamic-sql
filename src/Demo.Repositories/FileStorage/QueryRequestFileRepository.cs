using Demo.Contracts.Repositories;
using Demo.Contracts.Dto;
using System.Text.Json;

namespace Demo.Repositories.FileStorage;

public sealed class QueryRequestFileRepository(FilePathResolver filePathResolver)
    : FileRepositoryBase<QueryRequestDto>(filePathResolver, FOLDER_NAME, FILE_EXTENSION),
        IQueryRequestRepository
{
    private const string FOLDER_NAME = "QueryRequests";
    private const string FILE_EXTENSION = ".json";
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public override IReadOnlyCollection<QueryRequestDto> GetBuiltin()
    {
        var dir = GetBuiltinStoreFolder();
        Directory.CreateDirectory(dir);
        return Directory.EnumerateFiles(dir)
            .Where(IsJsonFile)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => ReadQueryRequest(path, isBuiltin: true))
            .ToArray();
    }

    public override IReadOnlyCollection<QueryRequestDto> GetUserDefined(string userSectionId)
    {
        var dir = GetUserStoreFolder(userSectionId);
        if (!Directory.Exists(dir))
            return [];
        return Directory.EnumerateFiles(dir)
            .Where(IsJsonFile)
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => ReadQueryRequest(path, isBuiltin: false))
            .ToArray();
    }

    public override QueryRequestDto? Get(string userSectionId, string id)
    {
        var path = GetUserFilePath(userSectionId, id);
        return File.Exists(path)
            ? ReadQueryRequest(path, isBuiltin: false)
            : null;
    }

    public override void Save(string userSectionId, QueryRequestDto queryRequest)
    {
        ArgumentNullException.ThrowIfNull(queryRequest);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryRequest.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryRequest.Entity);
        ArgumentNullException.ThrowIfNull(queryRequest.Projection);
        ArgumentNullException.ThrowIfNull(queryRequest.EntityAliases);

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

    private static bool IsJsonFile(string path)
        => string.Equals(Path.GetExtension(path), FILE_EXTENSION, StringComparison.OrdinalIgnoreCase);

    private static QueryRequestDto ReadQueryRequest(string path, bool isBuiltin)
    {
        var queryRequest = JsonSerializer.Deserialize<QueryRequestDto>(
            File.ReadAllText(path),
            JSON_OPTIONS)
            ?? throw new InvalidDataException($"Query request file '{path}' is empty.");
        if (string.IsNullOrWhiteSpace(queryRequest.Name) ||
            string.IsNullOrWhiteSpace(queryRequest.Entity) ||
            queryRequest.Projection is null ||
            queryRequest.EntityAliases is null)
            throw new InvalidDataException($"Query request file '{path}' is missing required data.");

        return queryRequest with { IsBuiltin = isBuiltin };
    }

    private static string Serialize(QueryRequestDto queryRequest)
        => JsonSerializer.Serialize(queryRequest, JSON_OPTIONS);
}
