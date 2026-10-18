using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

public static class QueryDefinitionParser
{
    public static QueryDefinition ParseFromYml(string yml)
    {
        ArgumentNullException.ThrowIfNull(yml);

        var definition = new DeserializerBuilder()
            .Build()
            .Deserialize<YamlQueryDefinition>(yml)
            ?? throw new FormatException("The YAML query definition cannot be null.");

        return new QueryDefinition
        {
            Name = definition.Name,
            Entity = !string.IsNullOrWhiteSpace(definition.Entity)
                ? definition.Entity
                : throw new FormatException("The YAML query definition requires an entity."),
            EntityAliases = EntityContextDefinitionParser.Parse(definition.EntityAliases ?? []),
            Fields = ProjectionDefinitionParser.Parse(definition.Fields),
            Filter = FilterDefinitionParser.Parse(definition.Filter),
            Sorts = SortDefinitionParser.Parse(definition.Sorts),
            Page = definition.Page,
            PageSize = definition.PageSize
        };
    }

    public static QueryDefinition ParseFromYml(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return ParseFromYml(File.ReadAllText(file.FullName));
    }

    public static QueryDefinition Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new QueryDefinition
        {
            Entity = root.GetProperty("entity").GetString()
                ?? throw new JsonException("The entity value cannot be null."),
            EntityAliases = root.TryGetProperty("entityAliases", out var aliases)
                ? aliases.Deserialize<List<EntityContextDefinition>>(
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new JsonException("Entity aliases cannot be null.")
                : [],
            Fields = root.TryGetProperty("fields", out var fields)
                ? fields.EnumerateArray()
                    .Select(field => JsonSerializer.Deserialize<ProjectionDefinition>(
                        field.GetRawText(),
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))
                        ?? throw new JsonException("A projection field cannot be null."))
                    .ToList()
                : [],
            Filter = root.TryGetProperty("filter", out var filter)
                ? filter.ValueKind == JsonValueKind.String
                    ? FilterDefinitionParser.Parse(filter.GetString() ?? string.Empty)
                    : JsonSerializer.Deserialize<FilterDefinition>(
                        filter,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))
                : null,
            Sorts = root.TryGetProperty("sorts", out var sorts)
                ? sorts.EnumerateArray().Select(ParseSort).ToList()
                : [],
            Page = ReadInt(root, "page"),
            PageSize = ReadInt(root, "pageSize")
        };
    }

    public static IFieldDto ParseField(string plainText) => FieldDefinitionParser.Parse(plainText);
    public static List<ProjectionDefinition> ParseProjections(string plainText) => ProjectionDefinitionParser.Parse(plainText);
    public static FilterDefinition? ParseFilter(string plainText) => FilterDefinitionParser.Parse(plainText);
    public static List<SortDefinition> ParseSorts(string plainText) => SortDefinitionParser.Parse(plainText);

    private static SortDefinition ParseSort(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString()
                ?? throw new JsonException("A sort cannot be null.");
            var descending = value.StartsWith("-", StringComparison.Ordinal);
            var fieldText = descending ? value[1..] : value;

            return new SortDefinition(
                FieldDefinitionParser.Parse(fieldText),
                descending ? QuerySortDirection.Descending : QuerySortDirection.Ascending);
        }

        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("A sort must be a string or an object.");

        var field = TryGetProperty(element, "field", out var fieldElement)
            ? ProjectionDefinitionJsonConverter.ParseField(fieldElement)
            : throw new JsonException("A sort requires a field expression.");
        var direction = !TryGetProperty(element, "direction", out var directionElement)
            ? QuerySortDirection.Ascending
            : ParseSortDirection(directionElement);

        return new SortDefinition(field, direction);
    }

    private static QuerySortDirection ParseSortDirection(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var value) &&
                Enum.IsDefined(typeof(QuerySortDirection), value) => (QuerySortDirection)value,
            JsonValueKind.String when Enum.TryParse<QuerySortDirection>(
                element.GetString(), true, out var value) => value,
            _ => throw new JsonException("A sort direction is invalid.")
        };

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value);

    private static int? ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
            return null;
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value))
            throw new JsonException($"The {name} value must be an integer or null.");
        return value;
    }

    private record YamlQueryDefinition()
    {
        public string? Name { get; set; }
        public string? Entity { get; set; }
        public List<string?>? EntityAliases { get; set; }
        public List<string?>? Fields { get; set; }
        public string? Filter { get; set; }
        public string? Sorts { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }
}
