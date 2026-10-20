using System.Text.Json;
using System.Text.Json.Serialization;

namespace Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

internal sealed class FilterDefinitionJsonConverter : JsonConverter<IFilterDefintion>
{
    public override IFilterDefintion Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return TryGetProperty(root, "filters", out _)
            ? JsonSerializer.Deserialize<FilterDefinition>(root, options)
                ?? throw new JsonException("The filter group cannot be null.")
            : JsonSerializer.Deserialize<FilterItemDefinition>(root, options)
                ?? throw new JsonException("The filter cannot be null.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        IFilterDefintion value,
        JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, value.GetType(), options);

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value);
}

internal sealed class GroupFilterDtoJsonConverter : JsonConverter<FilterDefinition>
{
    public override FilterDefinition Read(
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
            ? filterElement.Deserialize<List<IFilterDefintion>>(options) ?? []
            : [];

        return new FilterDefinition(logicalOperation, filters.ToArray());
    }

    public override void Write(
        Utf8JsonWriter writer,
        FilterDefinition value,
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

internal sealed class FilterItemDefinitionJsonConverter : JsonConverter<FilterItemDefinition>
{
    public override FilterItemDefinition Read(
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
        var operationElement = TryGetProperty(root, "operation", out var parsedOperation)
            ? parsedOperation
            : throw new JsonException("A filter item requires an operation.");
        var operatorId = operationElement.ValueKind == JsonValueKind.String
            ? operationElement.GetString()
            : null;
        if (operationElement.ValueKind is not (JsonValueKind.Number or JsonValueKind.String) ||
            (operationElement.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(operatorId)))
            throw new JsonException("A filter operation must be a numeric built-in operation or a custom operator ID.");
        var operation = operationElement.ValueKind == JsonValueKind.Number
            ? operationElement.Deserialize<BinaryOperator>(options)
            : BinaryOperator.Equals;
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

        return new FilterItemDefinition(logicalOperator, leftField, operation, right)
        {
            Parameterized = TryGetProperty(root, "parameterized", out var parameterized) &&
                parameterized.ValueKind == JsonValueKind.True,
            OperatorId = operatorId
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        FilterItemDefinition value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.LogicalOperator is { } logicalOperator)
            writer.WriteNumber("logicalOperator", (int)logicalOperator);
        else
            writer.WriteNull("logicalOperator");
        writer.WritePropertyName("left");
        JsonSerializer.Serialize<IFieldDefinition>(writer, value.Left, options);
        if (value.OperatorId is null)
            writer.WriteNumber("operation", (int)value.Operation);
        else
            writer.WriteString("operation", value.OperatorId);
        writer.WritePropertyName("right");
        if (value.Right is null)
            writer.WriteNullValue();
        else
            JsonSerializer.Serialize<IFieldDefinition>(writer, value.Right, options);
        writer.WriteBoolean("parameterized", value.Parameterized);
        writer.WriteEndObject();
    }

    private static IFieldDefinition ReadField(JsonElement element, JsonSerializerOptions options, string name)
        => element.ValueKind == JsonValueKind.String
            ? new FieldDefinition(element.GetString() ?? throw new JsonException($"A filter {name} cannot be null."))
            : JsonSerializer.Deserialize<IFieldDefinition>(element, options)
                ?? throw new JsonException($"A filter {name} expression cannot be null.");

    private static IFieldDefinition ReadLegacyValue(JsonElement element)
        => new ConstFieldDefinition(element.ValueKind switch
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
