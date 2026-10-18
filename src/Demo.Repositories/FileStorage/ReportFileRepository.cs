using Demo.Contracts.Repositories;
using Demo.Contracts.Dto;
using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Demo.Repositories.FileStorage;

public sealed class ReportFileRepository : RepositoryBase<ReportDto>, IReportRepository
{
    private const string FOLDER_NAME = "QueryRequests";
    private readonly FilePathResolver _filePathResolver;

    public ReportFileRepository(FilePathResolver filePathResolver)
    {
        _filePathResolver = filePathResolver;
    }

    public override IReadOnlyCollection<ReportDto> GetUserDefined(string userSectionId)
    {
        var dir = _filePathResolver.GetUserStoreFolder(userSectionId, FOLDER_NAME);
        Directory.CreateDirectory(dir);
        return Directory.EnumerateFiles(dir, "*.json")
            .OrderByDescending(Path.GetFileName)
            .Select(Read)
            .ToArray();
    }

    public override IReadOnlyCollection<ReportDto> GetBuiltin()
    {
        var dir = _filePathResolver.GetBuiltinStoreFolder(FOLDER_NAME);
        Directory.CreateDirectory(dir);
        return Directory.EnumerateFiles(dir)
            .Where(path => string.Equals(Path.GetExtension(path), ".yml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var requestText = File.ReadAllText(path);
                return new ReportDto(
                    Path.GetFileName(path)!,
                    QueryDefinitionParser.ParseFromYml(requestText),
                    requestText)
                {
                    CanDelete = false
                };
            })
            .ToArray();
    }

    public override ReportDto? Get(string userSectionId, string fileName)
    {
        var path = GetPath(userSectionId, fileName);
        return File.Exists(path) ? Read(path) : null;
    }

    public override void Save(string userSectionId, ReportDto report)
    {
        var dir = _filePathResolver.GetUserStoreFolder(userSectionId, FOLDER_NAME);
        Directory.CreateDirectory(dir);
        File.WriteAllText(GetPath(userSectionId, report.FileName), JsonSerializer.Serialize(
            new
            {
                Query = report.Query,
                RequestText = report.RequestText,
                Description = report.Description,
                ChartType = report.ChartType
            },
            Options(writeIndented: true)));
    }

    public override bool Delete(string userSectionId, string fileName)
    {
        var path = GetPath(userSectionId, fileName);
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    private ReportDto Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var queryElement = TryGetProperty(root, "Query", out var storedQuery) ? storedQuery : root;
        var requestText = TryGetProperty(root, "RequestText", out var text) &&
            text.ValueKind == JsonValueKind.String ? text.GetString() : null;
        var description = TryGetProperty(root, "Description", out var descriptionElement) &&
            descriptionElement.ValueKind == JsonValueKind.String ? descriptionElement.GetString() : null;
        var chartType = TryGetProperty(root, "ChartType", out var chartElement) &&
            chartElement.ValueKind == JsonValueKind.String ? chartElement.GetString() : null;
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

        return new ReportDto(Path.GetFileName(path)!, query, requestText, description, chartType);
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

    private string GetPath(string userSectionId, string fileName)
    {
        var dir = _filePathResolver.GetUserStoreFolder(userSectionId, FOLDER_NAME);
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
