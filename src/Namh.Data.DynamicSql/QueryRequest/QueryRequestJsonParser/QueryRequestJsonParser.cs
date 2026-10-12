using Namh.Data.DynamicSql;
using System.Text.Json;

namespace Namh.Data.DynamicSql.QueryRequestParsing;

internal static class QueryRequestJsonParser
{
    public static QueryRequest Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new QueryRequest
        {
            Entity = root.GetProperty("entity").GetString()
                ?? throw new JsonException("The entity value cannot be null."),
            Columns = root.TryGetProperty("columns", out var columns)
                ? columns.EnumerateArray().Select(ParseColumn).ToList()
                : [],
            Filter = root.TryGetProperty("filter", out var filter)
                ? filter.ValueKind == JsonValueKind.String
                    ? new QueryFilterParser(filter.GetString() ?? string.Empty).Parse()
                    : JsonSerializer.Deserialize<QueryFilter>(filter)
                : null,
            Sorts = root.TryGetProperty("sorts", out var sorts)
                ? sorts.EnumerateArray().Select(ParseSort).ToList()
                : [],
            Page = ReadInt(root, "page"),
            PageSize = ReadInt(root, "pageSize")
        };
    }

    private static QueryColumn ParseColumn(JsonElement element)
    {
        var value = element.GetString()
            ?? throw new JsonException("A column must be a string.");
        var separator = value.IndexOf('|');

        return separator < 0
            ? new QueryColumn(value)
            : new QueryColumn(value[..separator], value[(separator + 1)..]);
    }

    private static QuerySort ParseSort(JsonElement element)
    {
        var value = element.GetString()
            ?? throw new JsonException("A sort must be a string.");
        var descending = value.StartsWith("-", StringComparison.Ordinal);

        return new QuerySort(
            descending ? value[1..] : value,
            descending ? QuerySortDirection.Descending : QuerySortDirection.Ascending);
    }

    private static int? ReadInt(JsonElement root, string name)
        => root.TryGetProperty(name, out var element) ? element.GetInt32() : null;
}
