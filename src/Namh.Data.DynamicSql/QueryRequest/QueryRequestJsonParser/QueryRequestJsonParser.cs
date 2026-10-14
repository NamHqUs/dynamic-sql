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
            GroupBy = root.TryGetProperty("groupBy", out var groupBy)
                ? groupBy.EnumerateArray().Select(item => item.GetString()
                    ?? throw new JsonException("A group-by field must be a string.")).ToList()
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

    private static FieldTranslator ParseColumn(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var field = element.TryGetProperty("field", out var fieldElement)
                ? ReadOptionalString(fieldElement, "field")
                : null;
            var alias = element.TryGetProperty("alias", out var aliasElement)
                ? ReadOptionalString(aliasElement, "alias")
                : null;
            var expression = element.TryGetProperty("expression", out var expressionElement)
                ? JsonSerializer.Deserialize<FieldExpressionTransaltion>(expressionElement, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                : null;

            return new FieldTranslator(field, alias, expression);
        }

        var value = element.GetString()
            ?? throw new JsonException("A column must be a string or an object.");
        var separator = value.IndexOf('|');

        return separator < 0
            ? new FieldTranslator(value)
            : new FieldTranslator(value[..separator], value[(separator + 1)..]);
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

    private static string? ReadOptionalString(JsonElement element, string name)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Null => null,
            _ => throw new JsonException($"The {name} value must be a string or null.")
        };
}
