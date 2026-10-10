using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public sealed class QueryDefinition
{
    public required string Entity { get; init; }
    public List<QueryColumn> Columns { get; init; } = [];
    public QueryFilter? Filter { get; init; }
    public List<QuerySort> Sorts { get; init; } = [];
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public bool IncludeArchive { get; init; }

    public static QueryDefinition FromJson(string json)
        => QueryDefinitionJsonParser.Parse(json);

    public override string ToString()
        => string.Join(Environment.NewLine,
            $"Entity: {Entity}",
            $"Columns: {(Columns.Count == 0 ? "Empty" : string.Join(", ", Columns.Select(FormatColumn)))}",
            $"Filters: {(Filter is null || !Filter.Enabled ? "Empty" : FormatFilter(Filter, false))}",
            $"Sorts: {(Sorts.Count == 0 ? "Empty" : string.Join(", ", Sorts.Select(FormatSort)))}",
            $"Page: {Page?.ToString() ?? "Empty"}",
            $"PageSize: {PageSize?.ToString() ?? "Empty"}",
            $"IncludeArchive: {IncludeArchive}");

    private static string FormatColumn(QueryColumn column)
        => column.Alias is null ? column.Field : $"{column.Field} AS {column.Alias}";

    private static string FormatSort(QuerySort sort)
        => $"{sort.Field} {sort.Direction switch
        {
            QuerySortDirection.Ascending => "ASC",
            QuerySortDirection.Descending => "DESC",
            _ => throw new ArgumentOutOfRangeException(nameof(sort.Direction))
        }}";

    private static string FormatFilter(QueryFilter filter, bool parenthesize)
    {
        var text = filter switch
        {
            LogicalFilter logical =>
                $"{FormatFilter(logical.Left, true)} {FormatLogicalOperator(logical.Operator)} " +
                $"{FormatFilter(logical.Right, true)}",
            BinaryFilter binary =>
                $"{binary.Field} {FormatFilterOperator(binary.Operator)} {FormatValue(binary.Value)}",
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };

        return parenthesize && filter is LogicalFilter ? $"({text})" : text;
    }

    private static string FormatLogicalOperator(QueryFilterLogicalOperator @operator)
        => @operator switch
        {
            QueryFilterLogicalOperator.And => "AND",
            QueryFilterLogicalOperator.Or => "OR",
            _ => throw new ArgumentOutOfRangeException(nameof(@operator))
        };

    private static string FormatFilterOperator(QueryFilterOperator @operator)
        => @operator switch
        {
            QueryFilterOperator.Equals => "=",
            QueryFilterOperator.NotEquals => "<>",
            QueryFilterOperator.Like => "LIKE",
            QueryFilterOperator.GreaterThan => ">",
            QueryFilterOperator.LessThan => "<",
            QueryFilterOperator.GreaterThanOrEqual => ">=",
            QueryFilterOperator.LessThanOrEqual => "<=",
            _ => throw new ArgumentOutOfRangeException(nameof(@operator))
        };

    private static string FormatValue(object? value)
        => value switch
        {
            null => "NULL",
            string text => $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"",
            QueryFilterArithmetic arithmetic =>
                $"({FormatValue(arithmetic.Left)} {arithmetic.Operator} {arithmetic.Field})",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "NULL"
        };
}

public sealed record QueryColumn(string Field, string? Alias = null);

[JsonConverter(typeof(QueryFilterJsonConverter))]
public record QueryFilter(bool Enabled = true, bool Parameterized = false);

public record LogicalFilter(
    QueryFilter Left,
    QueryFilterLogicalOperator Operator,
    QueryFilter Right) : QueryFilter;

public record BinaryFilter(
    string Field,
    QueryFilterOperator Operator,
    object? Value = null) : QueryFilter;

public sealed record QueryFilterArithmetic(
    object? Left,
    string Operator,
    string Field);

public enum QueryFilterOperator
{
    Equals,
    NotEquals,
    Like,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual
}

public enum QueryFilterLogicalOperator
{
    And,
    Or
}

public sealed record QuerySort(
    string Field,
    QuerySortDirection Direction = QuerySortDirection.Ascending);

public enum QuerySortDirection
{
    Ascending,
    Descending
}

public sealed class QueryFilterJsonConverter : JsonConverter<QueryFilter>
{
    public override QueryFilter Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        var enabled = ReadBoolean(root, "enabled", true);
        var parameterized = ReadBoolean(root, "parameterized", false);

        if (TryGetProperty(root, "left", out var leftElement))
        {
            if (!TryGetProperty(root, "right", out var rightElement))
                throw new JsonException("A logical filter must contain both left and right filters.");

            var left = JsonSerializer.Deserialize<QueryFilter>(leftElement, options)
                ?? throw new JsonException("The logical filter left value cannot be null.");
            var right = JsonSerializer.Deserialize<QueryFilter>(rightElement, options)
                ?? throw new JsonException("The logical filter right value cannot be null.");

            return new LogicalFilter(
                left,
                ReadEnum<QueryFilterLogicalOperator>(root, "operator"),
                right)
            {
                Enabled = enabled,
                Parameterized = parameterized
            };
        }

        if (!TryGetProperty(root, "field", out var fieldElement) ||
            fieldElement.ValueKind != JsonValueKind.String)
            throw new JsonException("A binary filter must contain a field.");

        return new BinaryFilter(
            fieldElement.GetString()!,
            ReadEnum<QueryFilterOperator>(root, "operator"),
            root.TryGetProperty("value", out var valueElement) ? valueElement.Clone() : null)
        {
            Enabled = enabled,
            Parameterized = parameterized
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        QueryFilter value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("enabled", value.Enabled);
        writer.WriteBoolean("parameterized", value.Parameterized);

        switch (value)
        {
            case LogicalFilter logical:
                writer.WritePropertyName("left");
                JsonSerializer.Serialize(writer, logical.Left, options);
                writer.WriteNumber("operator", (int)logical.Operator);
                writer.WritePropertyName("right");
                JsonSerializer.Serialize(writer, logical.Right, options);
                break;
            case BinaryFilter binary:
                writer.WriteString("field", binary.Field);
                writer.WriteNumber("operator", (int)binary.Operator);
                writer.WritePropertyName("value");
                JsonSerializer.Serialize(writer, binary.Value, options);
                break;
            default:
                throw new JsonException($"Unsupported query filter type [{value.GetType().Name}].");
        }

        writer.WriteEndObject();
    }

    private static bool TryGetProperty(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
            return true;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool ReadBoolean(JsonElement element, string name, bool defaultValue)
        => TryGetProperty(element, name, out var value)
            ? value.GetBoolean()
            : defaultValue;

    private static TEnum ReadEnum<TEnum>(JsonElement element, string name)
        where TEnum : struct, Enum
    {
        if (!TryGetProperty(element, name, out var value))
            throw new JsonException($"A filter must contain [{name}].");

        return value.ValueKind == JsonValueKind.Number
            ? (TEnum)Enum.ToObject(typeof(TEnum), value.GetInt32())
            : Enum.Parse<TEnum>(value.GetString()!, true);
    }
}
