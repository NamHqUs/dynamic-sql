using Namh.Data.DynamicSql;
using System.Text.Json;
using System.Globalization;

namespace Demo.WebApi.Services;

public static class QueryRequestServices
{
    private static readonly string _StorePath = Path.Combine(Environment.CurrentDirectory, "DataStore", "QueryRequests");

    public sealed record StoredQueryRequest(string FileName, QueryRequest Query);

    public static List<StoredQueryRequest> ListQueryRequests()
    {
        EnsureStore();
        return Directory.EnumerateFiles(_StorePath, "*.json")
            .OrderByDescending(Path.GetFileName)
            .Select(filePath => new StoredQueryRequest(Path.GetFileName(filePath)!, ReadQuery(filePath)))
            .ToList();
    }

    public static StoredQueryRequest SaveQueryRequest(QueryRequest query, string? fileName = null)
    {
        fileName??= CreateRandonFileName();
        var filePath = GetPath(fileName);
        WriteQuery(filePath, query);
        return new StoredQueryRequest(fileName, query);
    }

    public static StoredQueryRequest? LoadQueryRequest(string fileName)
    {
        var filePath = GetPath(fileName);
        return File.Exists(filePath) ? new StoredQueryRequest(fileName, ReadQuery(filePath)) : null;
    }

    public static string GetQueryRequestString(QueryRequest query)
        => query.ToString();

    public static QueryRequest ParseQueryRequestString(string text, string entity)
    {
        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line =>
            {
                var separator = line.IndexOf(':');
                if (separator < 1)
                    throw new FormatException($"Invalid query request line [{line}].");
                var value = line[(separator + 1)..];
                if (value.StartsWith(' '))
                    value = value[1..];
                return (Key: line[..separator].Trim(), Value: value);
            })
            .ToList();
        if (lines.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new FormatException("Query request fields must not be repeated.");
        var values = lines.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        if (!values.TryGetValue("Entity", out var parsedEntity) || !string.Equals(parsedEntity, entity, StringComparison.Ordinal))
            throw new FormatException("Entity cannot be changed in the request structure.");

        var fieldText = values.GetValueOrDefault("Fields");
        var fields = !string.IsNullOrWhiteSpace(fieldText) && fieldText.TrimStart().StartsWith('[')
            ? ParseJsonFields(fieldText, parsedEntity)
            : ProjectionField.FromTextList(fieldText ?? string.Empty);
        var groupBy = ParseList(values.GetValueOrDefault("GroupBy"), item => item);
        var sorts = ParseList(values.GetValueOrDefault("Sorts"), item =>
        {
            var parts = item.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is < 1 or > 2)
                throw new FormatException($"Invalid sort definition [{item}].");
            var direction = parts.Length == 1 ? QuerySortDirection.Ascending : parts[1].ToUpperInvariant() switch
            {
                "ASC" => QuerySortDirection.Ascending,
                "DESC" => QuerySortDirection.Descending,
                _ => throw new FormatException($"Invalid sort direction [{parts[1]}].")
            };
            return new QuerySort(parts[0], direction);
        });
        var filterText = values.GetValueOrDefault("Filters");
        var filter = string.IsNullOrWhiteSpace(filterText)
            ? null
            : QueryRequest.FromJson(JsonSerializer.Serialize(new
            {
                entity = parsedEntity,
                filter = ConvertFilterExpression(filterText)
            })).Filter;

        var page = ParseNullableInt(values.GetValueOrDefault("Page"), "Page");
        var pageSize = ParseNullableInt(values.GetValueOrDefault("PageSize"), "PageSize");
        if (page is < 1 || pageSize is < 1)
            throw new FormatException("Page and PageSize must be greater than zero.");

        return new QueryRequest
        {
            Entity = parsedEntity,
            Fields = fields,
            GroupBy = groupBy,
            Filter = filter,
            Sorts = sorts,
            Page = page,
            PageSize = pageSize,
            IncludeArchive = bool.TryParse(values.GetValueOrDefault("IncludeArchive"), out var includeArchive)
                ? includeArchive
                : throw new FormatException("IncludeArchive must be True or False.")
        };
    }

    public static QueryRequest ParseQueryRequestString(string text)
    {
        var entities = text.Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line =>
            {
                var separator = line.IndexOf(':');
                return separator > 0 && line[..separator].Trim().Equals("Entity", StringComparison.OrdinalIgnoreCase)
                    ? line[(separator + 1)..].TrimStart()
                    : null;
            })
            .Where(entity => entity is not null)
            .ToList();
        if (entities.Count != 1 || string.IsNullOrWhiteSpace(entities[0]))
            throw new FormatException("Exactly one non-empty Entity field is required.");

        return ParseQueryRequestString(text, entities[0]!);
    }

    public static StoredQueryRequest? UpdateQueryRequest(string fileName, QueryRequest query)
    {
        var filePath = GetPath(fileName);
        if (!File.Exists(filePath))
            return null;

        WriteQuery(filePath, query);
        return new StoredQueryRequest(fileName, query);
    }

    public static bool DeleteQueryRequest(string fileName)
    {
        var filePath = GetPath(fileName);
        if (!File.Exists(filePath))
            return false;

        File.Delete(filePath);
        return true;
    }

    private static string CreateRandonFileName()
        => $"query_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json";

    private static string GetPath(string fileName)
        => Path.Combine(_StorePath, fileName);

    private static void EnsureStore()
        => Directory.CreateDirectory(_StorePath);

    private static QueryRequest ReadQuery(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var result = JsonSerializer.Deserialize<QueryRequest>(json, JsonOptions())
            ?? throw new InvalidDataException($"Query request file '{Path.GetFileName(filePath)}' is empty.");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!TryGetProperty(root, "Fields", out _) &&
            TryGetProperty(root, "Columns", out var legacyColumns))
        {
            result = new QueryRequest
            {
                Name = result.Name,
                Entity = result.Entity,
                Fields = legacyColumns.EnumerateArray().Select(ParseLegacyProjection).ToList(),
                GroupBy = result.GroupBy,
                Filter = result.Filter,
                Sorts = result.Sorts,
                Page = result.Page,
                PageSize = result.PageSize,
                IncludeArchive = result.IncludeArchive
            };
        }

        Console.WriteLine($"\n{new('-', 8)}Loaded query request from '{Path.GetFileName(filePath)}': \n{result}");
        return result;
    }

    private static ProjectionField ParseLegacyProjection(JsonElement column)
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
        var json = JsonSerializer.Serialize(new { field, alias });

        return JsonSerializer.Deserialize<ProjectionField>(json, JsonOptions())
            ?? throw new InvalidDataException("A saved projection field is empty.");
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToLowerInvariant(name[0]) + name[1..], out value);

    private static void WriteQuery(string filePath, QueryRequest query)
        => File.WriteAllText(filePath, JsonSerializer.Serialize(query, JsonOptions(writeIndented: true)));

    private static JsonSerializerOptions JsonOptions(bool writeIndented = false)
        => new()
        {
            WriteIndented = writeIndented,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter<DataType>() }
        };

    private static List<T> ParseList<T>(string? value, Func<string, T> parse)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(parse).ToList();

    private static List<ProjectionField> ParseJsonFields(string text, string entity)
    {
        using var document = JsonDocument.Parse(text);
        return QueryRequest.FromJson(JsonSerializer.Serialize(new
        {
            entity,
            fields = document.RootElement
        })).Fields;
    }

    private static int? ParseNullableInt(string? value, string fieldName)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? result
                : throw new FormatException($"{fieldName} must be an integer or blank.");

    private static string ConvertFilterExpression(string expression)
    {
        var converted = new System.Text.StringBuilder();
        for (var index = 0; index < expression.Length; index++)
        {
            var current = expression[index];
            if (current == '"')
            {
                converted.Append('\'');
                index++;
                while (index < expression.Length)
                {
                    if (expression[index] == '"' && index + 1 < expression.Length && expression[index + 1] == '"')
                    {
                        converted.Append('"');
                        index += 2;
                        continue;
                    }
                    if (expression[index] == '"')
                        break;
                    if (expression[index] == '\'')
                    {
                        converted.Append("''");
                        index++;
                    }
                    else
                    {
                        converted.Append(expression[index++]);
                    }
                }
                if (index >= expression.Length)
                    throw new FormatException("Unterminated quoted filter value.");
                converted.Append('\'');
                continue;
            }

            if (char.IsLetter(current))
            {
                var start = index;
                while (index + 1 < expression.Length && char.IsLetter(expression[index + 1]))
                    index++;
                var word = expression[start..(index + 1)];
                converted.Append(word.Equals("AND", StringComparison.OrdinalIgnoreCase) ? "&&"
                    : word.Equals("OR", StringComparison.OrdinalIgnoreCase) ? "||"
                    : word);
                continue;
            }

            if (current == '<' && index + 1 < expression.Length && expression[index + 1] == '>')
            {
                converted.Append("!=");
                index++;
                continue;
            }

            if (current == '=' && (index == 0 || expression[index - 1] is not '<' and not '>' and not '!')
                && (index + 1 >= expression.Length || expression[index + 1] != '='))
            {
                converted.Append("==");
                continue;
            }

            converted.Append(current);
        }

        return converted.ToString();
    }
}
