using System.Text.Json;
using System.Text.Json.Serialization;
using Demo.ServiceContracts.Repositories;
using Namh.Data.DynamicSql;

namespace Demo.Repositories.FileStorage;

public sealed class QueryRequestFileRepository : IQueryRequestRepository
{
    private readonly string _directory = Path.Combine(Environment.CurrentDirectory, "DataStore", "QueryRequests");

    public IReadOnlyList<StoredQueryRequest> GetAll()
    {
        Directory.CreateDirectory(_directory);
        return Directory.EnumerateFiles(_directory, "*.json")
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
        Directory.CreateDirectory(_directory);
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
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            throw new ArgumentException("Query request file name is invalid.", nameof(fileName));
        var path = Path.GetFullPath(Path.Combine(_directory, fileName));
        var root = Path.GetFullPath(_directory) + Path.DirectorySeparatorChar;
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
