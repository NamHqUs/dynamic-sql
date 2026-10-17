using Demo.ServiceContracts.Repositories;
using Namh.Data.DynamicSql;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Demo.Repositories.FileStorage;

public sealed class QueryRequestFileRepository : IQueryRequestRepository
{
    private readonly static object _lock = new();
    private static List<StoredQueryRequest> _builtinItems = null!;
    private const string FOLDER_NAME = "QueryRequests";
    private readonly FilePathResolver _filePathResolver;    

    public QueryRequestFileRepository(FilePathResolver filePathResolver)
    {
        _filePathResolver = filePathResolver;

        if (_builtinItems == null)
            lock (_lock)
            {
                var builtinFolder = _filePathResolver.GetBuiltinStoreFolder(FOLDER_NAME);
                _builtinItems ??= GetAll(builtinFolder).ToList();
            }
    }

    public IReadOnlyList<StoredQueryRequest> GetAll()
    {
        var customQueryRequests = GetAll(_filePathResolver.GetUserStoreFolder(FOLDER_NAME));
        return new List<StoredQueryRequest>(_builtinItems).Concat(customQueryRequests).ToList();
    }

    private IReadOnlyList<StoredQueryRequest> GetAll(string dir)
    {
        Directory.CreateDirectory(dir);
        return Directory.EnumerateFiles(dir, "*.json")
            .OrderByDescending(Path.GetFileName)
            .Select(Read)
            .ToArray();
    }

    public StoredQueryRequest? Get(string fileName)
    {
        var path = GetPath(fileName);
        return File.Exists(path) ? Read(path) : null;
    }

    public void Save(StoredQueryRequest request)
    {
        var dir = _filePathResolver.GetUserStoreFolder(FOLDER_NAME);
        Directory.CreateDirectory(dir);
        File.WriteAllText(GetPath(request.FileName), JsonSerializer.Serialize(
            new { Query = request.Query, RequestText = request.RequestText },
            Options(writeIndented: true)));
    }

    public bool Delete(string fileName)
    {
        var path = GetPath(fileName);
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

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
                PageSize = query.PageSize,
                IncludeArchive = query.IncludeArchive
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
