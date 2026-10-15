using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.QueryRequestParsing;
using System.Text.Json;

namespace Namh.Data.DynamicSql.QueryDtoParsing;

internal static class QueryDtoParser
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
                    .Select(field => JsonSerializer.Deserialize<ProjectionField>(
                        field.GetRawText(),
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))
                        ?? throw new JsonException("A projection field cannot be null."))
                    .ToList()
                : [],
            GroupBy = root.TryGetProperty("groupBy", out var groupBy)
                ? groupBy.EnumerateArray().Select(item => item.GetString()
                    ?? throw new JsonException("A group-by field must be a string.")).ToList()
                : [],
            Filter = root.TryGetProperty("filter", out var filter)
                ? filter.ValueKind == JsonValueKind.String
                    ? new FilterDtoParser(filter.GetString() ?? string.Empty).Parse()
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
        var value = element.GetString()
            ?? throw new JsonException("A sort must be a string.");
        var descending = value.StartsWith("-", StringComparison.Ordinal);

        return new SortDto(
            descending ? value[1..] : value,
            descending ? QuerySortDirection.Descending : QuerySortDirection.Ascending);
    }

    private static int? ReadInt(JsonElement root, string name)
        => root.TryGetProperty(name, out var element) ? element.GetInt32() : null;

}
