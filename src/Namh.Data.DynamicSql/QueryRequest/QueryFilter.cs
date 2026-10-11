using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

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
