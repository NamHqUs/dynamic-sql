using Demo.Contracts.Repositories;
using Demo.Contracts.Dto;
using Namh.Data.DynamicSql;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        var json = NormalizeLegacyQueryRequest(File.ReadAllText(path));
        var queryRequest = JsonSerializer.Deserialize<QueryRequestDto>(
            json,
            JSON_OPTIONS)
            ?? throw new InvalidDataException($"Query request file '{path}' is empty.");
        if (string.IsNullOrWhiteSpace(queryRequest.Name) ||
            string.IsNullOrWhiteSpace(queryRequest.Entity) ||
            queryRequest.Projection is null ||
            queryRequest.EntityAliases is null)
            throw new InvalidDataException($"Query request file '{path}' is missing required data.");

        return queryRequest with { IsBuiltin = isBuiltin };
    }

    private static string NormalizeLegacyQueryRequest(string json)
    {
        var root = JsonNode.Parse(json);
        if (root is not JsonObject queryRequest)
            return json;

        var wasNormalized = false;
        var projectionProperty = queryRequest.FirstOrDefault(property =>
            string.Equals(property.Key, "Projection", StringComparison.OrdinalIgnoreCase));
        if (projectionProperty.Value is JsonArray groups && groups.Any(group => group is JsonArray))
        {
            var flattened = new JsonArray();
            var hasGroup = false;
            foreach (var group in groups)
            {
                if (group is not JsonArray projections)
                    throw new JsonException("A legacy projection must contain only groups.");
                if (projections.Count == 0)
                    continue;

                if (hasGroup)
                {
                    flattened.Add(new JsonObject
                    {
                        ["field"] = new JsonObject
                        {
                            ["kind"] = "field",
                            ["field"] = string.Empty
                        },
                        ["alias"] = ProjectionDefinition.NewLineAlias
                    });
                }

                foreach (var projection in projections)
                    flattened.Add(projection?.DeepClone());
                hasGroup = true;
            }

            queryRequest[projectionProperty.Key] = flattened;
            wasNormalized = true;
        }

        var filtersProperty = queryRequest.FirstOrDefault(property =>
            string.Equals(property.Key, "Filters", StringComparison.OrdinalIgnoreCase));
        if (filtersProperty.Value is JsonArray filters)
        {
            queryRequest[filtersProperty.Key] = filters.Count switch
            {
                0 => null,
                1 => filters[0]?.DeepClone(),
                _ => new JsonObject
                {
                    ["logicalOperation"] = 0,
                    ["filters"] = new JsonArray(filters.Select(filter => filter?.DeepClone()).ToArray())
                }
            };
            wasNormalized = true;
        }

        return wasNormalized ? queryRequest.ToJsonString() : json;
    }

    private static string Serialize(QueryRequestDto queryRequest)
        => JsonSerializer.Serialize(queryRequest, JSON_OPTIONS);
}
