using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;
using System.Text.Json;
using System.Globalization;

namespace Demo.WebApi.Services;

public static class QueryRequestServices
{
    private static readonly string _StorePath = Path.Combine(Environment.CurrentDirectory, "DataStore", "QueryRequests");

    public sealed record StoredQueryRequest(string FileName, QueryDto Query, string? RequestText = null);

    public static List<StoredQueryRequest> ListQueryRequests()
    {
        EnsureStore();
        return Directory.EnumerateFiles(_StorePath, "*.json")
            .OrderByDescending(Path.GetFileName)
            .Select(ReadQuery)
            .ToList();
    }

    public static StoredQueryRequest SaveQueryRequest(
        QueryDto query,
        string? fileName = null,
        string? requestText = null)
    {
        fileName??= CreateRandonFileName();
        var filePath = GetPath(fileName);
        WriteQuery(filePath, query, requestText);
        return new StoredQueryRequest(fileName, query, requestText);
    }

    public static StoredQueryRequest? LoadQueryRequest(string fileName)
    {
        var filePath = GetPath(fileName);
        return File.Exists(filePath) ? ReadQuery(filePath) : null;
    }

    public static string GetQueryRequestString(QueryDto query)
        => query.ToString();

    public static QueryDto ParseQueryRequestString(string text, string entity)
    {
        var lines = new List<(string Key, string Value)>();
        foreach (var line in text.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var separator = line.IndexOf(':');
            if (separator < 1)
            {
                if (lines.Count == 0)
                    throw new FormatException($"Invalid query request line [{line}].");
                var previous = lines[^1];
                lines[^1] = (previous.Key, $"{previous.Value}\n{line}");
                continue;
            }

            var value = line[(separator + 1)..];
            if (value.StartsWith(' '))
                value = value[1..];
            lines.Add((line[..separator].Trim(), value));
        }
        if (lines.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new FormatException("Query request fields must not be repeated.");
        var values = lines.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        if (!values.TryGetValue("Entity", out var parsedEntity) || !string.Equals(parsedEntity, entity, StringComparison.Ordinal))
            throw new FormatException("Entity cannot be changed in the request structure.");

        var fieldText = values.GetValueOrDefault("Fields");
        var fields = !string.IsNullOrWhiteSpace(fieldText) && fieldText.TrimStart().StartsWith('[')
            ? ParseJsonFields(fieldText, parsedEntity)
            : QueryDtoParser.ParseProjections(fieldText ?? string.Empty);
        var sorts = ParseSorts(values.GetValueOrDefault("Sorts"));

        var filterText = values.GetValueOrDefault("Filters");
        var filter = string.IsNullOrWhiteSpace(filterText)
            ? null
            : QueryDto.FromJson(JsonSerializer.Serialize(new
            {
                entity = parsedEntity,
                filter = ConvertFilterExpression(filterText)
            })).Filter;

        var page = ParseNullableInt(values.GetValueOrDefault("Page"), "Page");
        var pageSize = ParseNullableInt(values.GetValueOrDefault("PageSize"), "PageSize");
        if (page is < 1 || pageSize is < 1)
            throw new FormatException("Page and PageSize must be greater than zero.");

        return new QueryDto
        {
            Entity = parsedEntity,
            Fields = fields,
            Filter = filter,
            Sorts = sorts,
            Page = page,
            PageSize = pageSize,
            IncludeArchive = bool.TryParse(values.GetValueOrDefault("IncludeArchive"), out var includeArchive)
                ? includeArchive
                : throw new FormatException("IncludeArchive must be True or False.")
        };
    }

    public static QueryDto ParseQueryRequestString(string text)
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

    public static StoredQueryRequest? UpdateQueryRequest(string fileName, QueryDto query, string? requestText = null)
    {
        var filePath = GetPath(fileName);
        if (!File.Exists(filePath))
            return null;

        WriteQuery(filePath, query, requestText);
        return new StoredQueryRequest(fileName, query, requestText);
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

    private static StoredQueryRequest ReadQuery(string filePath)
    {
        var json = File.ReadAllText(filePath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var queryElement = TryGetProperty(root, "Query", out var storedQuery) ? storedQuery : root;
        var requestText = TryGetProperty(root, "RequestText", out var requestTextElement)
            && requestTextElement.ValueKind == JsonValueKind.String
                ? requestTextElement.GetString()
                : null;
        var result = JsonSerializer.Deserialize<QueryDto>(queryElement.GetRawText(), JsonOptions())
            ?? throw new InvalidDataException($"Query request file '{Path.GetFileName(filePath)}' is empty.");

        if (!TryGetProperty(queryElement, "Fields", out _) &&
            TryGetProperty(queryElement, "Columns", out var legacyColumns))
        {
            result = new QueryDto
            {
                Name = result.Name,
                Entity = result.Entity,
                Fields = legacyColumns.EnumerateArray().Select(ParseLegacyProjection).ToList(),
                Filter = result.Filter,
                Sorts = result.Sorts,
                Page = result.Page,
                PageSize = result.PageSize,
                IncludeArchive = result.IncludeArchive
            };
        }

        Console.WriteLine($"\n{new('-', 8)}Loaded query request from '{Path.GetFileName(filePath)}': \n{result}");
        return new StoredQueryRequest(Path.GetFileName(filePath)!, result, requestText);
    }

    private static ProjectionDto ParseLegacyProjection(JsonElement column)
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

        return JsonSerializer.Deserialize<ProjectionDto>(json, JsonOptions())
            ?? throw new InvalidDataException("A saved projection field is empty.");
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToLowerInvariant(name[0]) + name[1..], out value);

    private static void WriteQuery(string filePath, QueryDto query, string? requestText)
        => File.WriteAllText(filePath, JsonSerializer.Serialize(
            new { Query = query, RequestText = requestText },
            JsonOptions(writeIndented: true)));

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

    private static List<SortDto> ParseSorts(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : SplitTopLevel(value, ',').Select(item =>
            {
                var sortText = item.Trim();
                var direction = QuerySortDirection.Ascending;
                if (sortText.EndsWith(" ASC", StringComparison.OrdinalIgnoreCase))
                {
                    sortText = sortText[..^4].TrimEnd();
                }
                else if (sortText.EndsWith(" DESC", StringComparison.OrdinalIgnoreCase))
                {
                    sortText = sortText[..^5].TrimEnd();
                    direction = QuerySortDirection.Descending;
                }

                if (string.IsNullOrWhiteSpace(sortText))
                    throw new FormatException($"Invalid sort definition [{item}].");

                return new SortDto(QueryDtoParser.ParseField(sortText), direction);
            }).ToList();

    private static List<string> SplitTopLevel(string text, char delimiter)
    {
        var parts = new List<string>();
        var start = 0;
        var parentheses = 0;
        var brackets = 0;
        var constraints = 0;

        for (var index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '(':
                    parentheses++;
                    break;
                case ')':
                    parentheses--;
                    break;
                case '[':
                    brackets++;
                    break;
                case ']':
                    brackets--;
                    break;
                case '<':
                    constraints++;
                    break;
                case '>':
                    if (constraints > 0)
                        constraints--;
                    break;
            }

            if (text[index] == delimiter && parentheses == 0 && brackets == 0 && constraints == 0)
            {
                parts.Add(text[start..index].Trim());
                start = index + 1;
            }
        }

        parts.Add(text[start..].Trim());
        return parts.Where(part => part.Length > 0).ToList();
    }

    private static List<ProjectionDto> ParseJsonFields(string text, string entity)
    {
        using var document = JsonDocument.Parse(text);
        return QueryDto.FromJson(JsonSerializer.Serialize(new
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
