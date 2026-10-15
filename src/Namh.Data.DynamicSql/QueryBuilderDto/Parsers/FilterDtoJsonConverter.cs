using System.Text.Json;
using System.Text.Json.Serialization;

namespace Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

internal sealed class FilterDtoJsonConverter : JsonConverter<IFilterDto>
{
    public override IFilterDto Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return TryGetProperty(root, "filters", out _)
            ? JsonSerializer.Deserialize<FilterDto>(root, options)
                ?? throw new JsonException("The filter group cannot be null.")
            : JsonSerializer.Deserialize<FilterItemDto>(root, options)
                ?? throw new JsonException("The filter cannot be null.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        IFilterDto value,
        JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, value.GetType(), options);

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value);
}

internal sealed class GroupFilterDtoJsonConverter : JsonConverter<FilterDto>
{
    public override FilterDto Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var logicalOperation = TryGetProperty(root, "logicalOperation", out var operation)
            ? operation.Deserialize<LogicalOperator>(options)
            : LogicalOperator.And;
        var filters = TryGetProperty(root, "filters", out var filterElement)
            ? filterElement.Deserialize<List<IFilterDto>>(options) ?? []
            : [];

        return new FilterDto(logicalOperation, filters.ToArray());
    }

    public override void Write(
        Utf8JsonWriter writer,
        FilterDto value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("logicalOperation", (int)value.LogicalOperation);
        writer.WritePropertyName("filters");
        JsonSerializer.Serialize(writer, value.Filters, options);
        writer.WriteEndObject();
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value);
}

internal sealed class FilterItemDtoJsonConverter : JsonConverter<FilterItemDto>
{
    public override FilterItemDto Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var leftElement = TryGetProperty(root, "left", out var left)
            ? left
            : TryGetProperty(root, "field", out left)
                ? left
                : throw new JsonException("A filter item requires a left field expression.");
        var leftField = ReadField(leftElement, options, "left");
        var operation = TryGetProperty(root, "operation", out var operationElement)
            ? operationElement.Deserialize<BinaryOperator>(options)
            : throw new JsonException("A filter item requires an operation.");
        var right = TryGetProperty(root, "right", out var rightElement)
            ? rightElement.ValueKind == JsonValueKind.Null
                ? null
                : ReadField(rightElement, options, "right")
            : TryGetProperty(root, "value", out var legacyValue)
                ? ReadLegacyValue(legacyValue)
                : null;
        LogicalOperator? logicalOperator = TryGetProperty(root, "logicalOperator", out var logicalElement) &&
            logicalElement.ValueKind != JsonValueKind.Null
                ? logicalElement.Deserialize<LogicalOperator>(options)
                : null;

        return new FilterItemDto(logicalOperator, leftField, operation, right)
        {
            Parameterized = TryGetProperty(root, "parameterized", out var parameterized) &&
                parameterized.ValueKind == JsonValueKind.True
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        FilterItemDto value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.LogicalOperator is { } logicalOperator)
            writer.WriteNumber("logicalOperator", (int)logicalOperator);
        else
            writer.WriteNull("logicalOperator");
        writer.WritePropertyName("left");
        JsonSerializer.Serialize<IFieldDto>(writer, value.Left, options);
        writer.WriteNumber("operation", (int)value.Operation);
        writer.WritePropertyName("right");
        if (value.Right is null)
            writer.WriteNullValue();
        else
            JsonSerializer.Serialize<IFieldDto>(writer, value.Right, options);
        writer.WriteBoolean("parameterized", value.Parameterized);
        writer.WriteEndObject();
    }

    private static IFieldDto ReadField(JsonElement element, JsonSerializerOptions options, string name)
        => element.ValueKind == JsonValueKind.String
            ? new FieldDto(element.GetString() ?? throw new JsonException($"A filter {name} cannot be null."))
            : JsonSerializer.Deserialize<IFieldDto>(element, options)
                ?? throw new JsonException($"A filter {name} expression cannot be null.");

    private static IFieldDto ReadLegacyValue(JsonElement element)
        => new ConstFieldDto(element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDecimal(),
            _ => throw new JsonException($"Unsupported legacy filter value [{element.ValueKind}].")
        });

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value);
}
