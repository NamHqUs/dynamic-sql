using Namh.Data.DynamicSql;
using System.Text.Json;

namespace Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

public static class QueryDtoParser
{
    public static QueryDto Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new QueryDto
        {
            Entity = root.GetProperty("entity").GetString()
                ?? throw new JsonException("The entity value cannot be null."),
            Fields = root.TryGetProperty("fields", out var fields)
                ? fields.EnumerateArray()
                    .Select(field => JsonSerializer.Deserialize<ProjectionDto>(
                        field.GetRawText(),
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))
                        ?? throw new JsonException("A projection field cannot be null."))
                    .ToList()
                : [],
            Filter = root.TryGetProperty("filter", out var filter)
                ? filter.ValueKind == JsonValueKind.String
                    ? FilterDtoParser.Parse(filter.GetString() ?? string.Empty)
                    : JsonSerializer.Deserialize<FilterDto>(
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

    private static SortDto ParseSort(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString()
                ?? throw new JsonException("A sort cannot be null.");
            var descending = value.StartsWith("-", StringComparison.Ordinal);
            var fieldText = descending ? value[1..] : value;

            return new SortDto(
                FieldDtoParser.Parse(fieldText),
                descending ? QuerySortDirection.Descending : QuerySortDirection.Ascending);
        }

        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("A sort must be a string or an object.");

        var field = TryGetProperty(element, "field", out var fieldElement)
            ? ProjectionDtoJsonConverter.ParseField(fieldElement)
            : throw new JsonException("A sort requires a field expression.");
        var direction = !TryGetProperty(element, "direction", out var directionElement)
            ? QuerySortDirection.Ascending
            : ParseSortDirection(directionElement);

        return new SortDto(field, direction);
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

}
