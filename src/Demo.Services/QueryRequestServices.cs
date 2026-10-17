using Demo.ServiceContracts.Repositories;
using Demo.ServiceContracts.Services;
using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
using System.Globalization;
using System.Text.Json;

namespace Demo.Services;

public sealed class QueryRequestService(IQueryRequestRepository repository) : IQueryRequestService
{
    public IReadOnlyList<StoredQueryRequest> ListQueryRequests() => repository.GetAll();

    public StoredQueryRequest SaveQueryRequest(
        QueryDefinition query,
        string? fileName = null,
        string? requestText = null)
    {
        fileName ??= CreateRandomFileName();
        var request = new StoredQueryRequest(fileName, query, requestText);
        repository.Save(request);
        return request;
    }

    public StoredQueryRequest? LoadQueryRequest(string fileName) => repository.Get(fileName);

    public string GetQueryRequestString(QueryDefinition query)
        => query.ToString();

    public QueryDefinition ParseQueryRequestString(string text, string entity)
    {
        var lines = new List<(string Key, string Value)>();
        foreach (var line in text.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (line.Length > 0 && char.IsWhiteSpace(line[0]))
            {
                if (lines.Count == 0)
                    throw new FormatException($"Invalid query request line [{line}].");
                var previous = lines[^1];
                lines[^1] = (previous.Key, $"{previous.Value}\n{line.Trim()}");
                continue;
            }

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

        var parsedEntities = values.GetValueOrDefault("Entities");
        var parsedEntity = values.TryGetValue("Entity", out var legacyEntity)
            ? legacyEntity
            : ParseEntityList(parsedEntities).Entity;
        if (!string.Equals(parsedEntity, entity, StringComparison.Ordinal))
            throw new FormatException("Entity cannot be changed in the request structure.");
        if (values.ContainsKey("Entity") && values.ContainsKey("Entities"))
            throw new FormatException("Use either Entity or Entities, not both.");

        var fieldText = values.GetValueOrDefault("Fields");
        var fields = QueryDefinitionParser.ParseProjections(fieldText ?? string.Empty);
        var sorts = ParseSorts(values.GetValueOrDefault("Sorts"));

        var filterText = values.GetValueOrDefault("Filters");
        var filter = string.IsNullOrWhiteSpace(filterText)
            ? null
            : QueryDefinitionParser.ParseFilter(ConvertFilterExpression(filterText));

        var page = ParseNullableInt(values.GetValueOrDefault("Page"), "Page");
        var pageSize = ParseNullableInt(values.GetValueOrDefault("PageSize"), "PageSize");
        if (page is < 1 || pageSize is < 1)
            throw new FormatException("Page and PageSize must be greater than zero.");

        return new QueryDefinition
        {
            Entity = parsedEntity,
            EntityAliases = string.IsNullOrWhiteSpace(parsedEntities)
                ? []
                : ParseEntityList(parsedEntities).Aliases,
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

    public QueryDefinition ParseQueryRequestString(string text)
    {
        var entity = text.Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith("Entity:", StringComparison.OrdinalIgnoreCase));
        if (entity is not null)
            return ParseQueryRequestString(text, entity[(entity.IndexOf(':') + 1)..].Trim());

        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
        var entitiesHeader = Array.FindIndex(lines, line =>
            line.Trim().Equals("Entities:", StringComparison.OrdinalIgnoreCase));
        var rootEntity = entitiesHeader < 0
            ? null
            : lines.Skip(entitiesHeader + 1)
                .TakeWhile(line => string.IsNullOrWhiteSpace(line) || char.IsWhiteSpace(line[0]))
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0);
        if (string.IsNullOrWhiteSpace(rootEntity))
            throw new FormatException("Exactly one non-empty Entity or Entities root is required.");

        return ParseQueryRequestString(text, rootEntity);
    }

    private static (string Entity, List<EntityContextDefinition> Aliases) ParseEntityList(string? value)
    {
        var entries = (value ?? string.Empty)
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length == 0)
            throw new FormatException("Entities requires a root entity.");

        var aliases = new List<EntityContextDefinition>();
        foreach (var entry in entries.Skip(1))
        {
            var separator = entry.IndexOf(':');
            if (separator < 1)
                throw new FormatException($"Invalid entity alias definition [{entry}].");

            var alias = entry[..separator].Trim();
            var relation = entry[(separator + 1)..].Trim();
            var keyStart = relation.LastIndexOf('(');
            if (keyStart < 1 || !relation.EndsWith(')'))
                throw new FormatException($"Entity alias [{alias}] requires relation key mappings.");

            var relationPath = relation[..keyStart].Trim();
            var relatedKeys = SplitTopLevel(relation[(keyStart + 1)..^1], ',')
                .Select(keyText =>
                {
                    var keySeparator = keyText.IndexOf(':');
                    var fromColumn = keySeparator < 0 ? keyText.Trim() : keyText[..keySeparator].Trim();
                    var toColumn = keySeparator < 0 ? fromColumn : keyText[(keySeparator + 1)..].Trim();
                    if (string.IsNullOrWhiteSpace(fromColumn) || string.IsNullOrWhiteSpace(toColumn))
                        throw new FormatException($"Invalid relation key mapping [{keyText}].");
                    return new RelationItem(fromColumn, toColumn);
                })
                .ToList();
            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(relationPath) || relatedKeys.Count == 0)
                throw new FormatException($"Invalid entity alias definition [{entry}].");

            aliases.Add(new EntityContextDefinition(alias, relationPath, relatedKeys));
        }

        return (entries[0], aliases);
    }

    public StoredQueryRequest? UpdateQueryRequest(string fileName, QueryDefinition query, string? requestText = null)
    {
        if (repository.Get(fileName) is null)
            return null;

        var request = new StoredQueryRequest(fileName, query, requestText);
        repository.Save(request);
        return request;
    }

    public bool DeleteQueryRequest(string fileName) => repository.Delete(fileName);

    private static string CreateRandomFileName()
        => $"query_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json";

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

    private static List<SortDefinition> ParseSorts(string? value)
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

                return new SortDefinition(QueryDefinitionParser.ParseField(sortText), direction);
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
