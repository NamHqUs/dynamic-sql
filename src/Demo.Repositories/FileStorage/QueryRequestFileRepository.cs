using Demo.Contracts.Repositories;
using Namh.Data.DynamicSql;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Demo.Repositories.FileStorage;

public sealed class QueryRequestFileRepository : IQueryRequestRepository
{
    private const string FOLDER_NAME = "QueryRequests";
    private readonly FilePathResolver _filePathResolver;

    public QueryRequestFileRepository(FilePathResolver filePathResolver)
    {
        _filePathResolver = filePathResolver;
    }

    public IReadOnlyList<StoredQueryRequest> GetAllCustom()
        => GetAll(_filePathResolver.GetUserStoreFolder(FOLDER_NAME));

    public IReadOnlyList<BuiltinQueryRequestFile> GetBuiltinFiles()
    {
        var dir = _filePathResolver.GetBuiltinStoreFolder(FOLDER_NAME);
        Directory.CreateDirectory(dir);
        return Directory.EnumerateFiles(dir)
            .Where(path => string.Equals(Path.GetExtension(path), ".yml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => new BuiltinQueryRequestFile(Path.GetFileName(path)!, File.ReadAllText(path)))
            .ToArray();
    }

    private IReadOnlyList<StoredQueryRequest> GetAll(string dir)
    {
        Directory.CreateDirectory(dir);
        var requests = Directory.EnumerateFiles(dir, "*.json")
            .OrderByDescending(Path.GetFileName)
            .Select(Read)
            .ToArray();
        var builtinFileNames = GetBuiltinFiles()
            .Select(request => request.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflictingRequest = requests.FirstOrDefault(request => builtinFileNames.Contains(request.FileName));
        if (conflictingRequest is not null)
            throw new InvalidDataException(
                $"User query request '{conflictingRequest.FileName}' cannot replace a built-in request.");
        return requests;
    }

    public StoredQueryRequest? Get(string fileName)
    {
        if (IsBuiltinFileName(fileName))
            return null;

        var path = GetPath(fileName);
        return File.Exists(path) ? Read(path) : null;
    }

    public void Save(StoredQueryRequest request)
    {
        if (IsBuiltinFileName(request.FileName))
            throw new InvalidOperationException($"Built-in query request '{request.FileName}' cannot be changed.");

        var dir = _filePathResolver.GetUserStoreFolder(FOLDER_NAME);
        Directory.CreateDirectory(dir);
        File.WriteAllText(GetPath(request.FileName), JsonSerializer.Serialize(
            new { Query = request.Query, RequestText = request.RequestText },
            Options(writeIndented: true)));
    }

    public bool Delete(string fileName)
    {
        if (IsBuiltinFileName(fileName))
            return false;

        var path = GetPath(fileName);
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    private bool IsBuiltinFileName(string fileName)
        => GetBuiltinFiles().Any(request =>
            string.Equals(request.FileName, fileName, StringComparison.OrdinalIgnoreCase));

    private StoredQueryRequest Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var queryElement = TryGetProperty(root, "Query", out var storedQuery) ? storedQuery : root;
        var requestText = TryGetProperty(root, "RequestText", out var text) &&
            text.ValueKind == JsonValueKind.String ? text.GetString() : null;
        var query = JsonSerializer.Deserialize<QueryDefinition>(queryElement.GetRawText(), Options())
            ?? throw new InvalidDataException($"Query request file '{Path.GetFileName(path)}' is empty.");

        if (!TryGetProperty(queryElement, "Fields", out _) &&
            TryGetProperty(queryElement, "Columns", out var legacyColumns))
        {
            query = new QueryDefinition
            {
                Name = query.Name,
                Entity = query.Entity,
                Fields = legacyColumns.EnumerateArray().Select(ParseLegacyProjection).ToList(),
                Filter = query.Filter,
                Sorts = query.Sorts,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        return new StoredQueryRequest(Path.GetFileName(path)!, query, requestText);
    }

    private static ProjectionDefinition ParseLegacyProjection(JsonElement column)
    {
        var alias = TryGetProperty(column, "Alias", out var aliasElement)
            ? aliasElement.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => aliasElement.GetString(),
                _ => throw new InvalidDataException("A saved projection alias must be a string or null.")
            }
            : null;
        object field = TryGetProperty(column, "Expression", out var expressionElement) &&
            expressionElement.ValueKind != JsonValueKind.Null
                ? expressionElement.Clone()
                : TryGetProperty(column, "Field", out var fieldElement) &&
                    fieldElement.ValueKind == JsonValueKind.String
                    ? new { kind = "field", field = fieldElement.GetString() }
                    : throw new InvalidDataException("A saved projection must contain a field or expression.");
        return JsonSerializer.Deserialize<ProjectionDefinition>(
            JsonSerializer.Serialize(new { field, alias }), Options())
            ?? throw new InvalidDataException("A saved projection field is empty.");
    }

    private string GetPath(string fileName)
    {
        var dir = _filePathResolver.GetUserStoreFolder(FOLDER_NAME);
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            throw new ArgumentException("Query request file name is invalid.", nameof(fileName));
        var path = Path.GetFullPath(Path.Combine(dir, fileName));
        var root = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Query request file name is invalid.", nameof(fileName));
        return path;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToLowerInvariant(name[0]) + name[1..], out value);

    private static JsonSerializerOptions Options(bool writeIndented = false)
        => new()
        {
            WriteIndented = writeIndented,
            Converters = { new JsonStringEnumConverter<DataType>() }
        };
}
