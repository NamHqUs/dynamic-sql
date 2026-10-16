using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

public sealed class ProjectionDefinitionJsonConverter : JsonConverter<ProjectionDefinition>
{
    public override ProjectionDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("A projection field must be an object.");

        var field = TryGetProperty(root, "field", out var fieldElement)
            ? ParseField(fieldElement)
            : throw new JsonException("A projection field requires a field expression.");
        var alias = TryGetProperty(root, "alias", out var aliasElement)
            ? aliasElement.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => aliasElement.GetString(),
                _ => throw new JsonException("A projection field alias must be a string or null.")
            }
            : null;

        return new ProjectionDefinition(field, alias);
    }

    public override void Write(Utf8JsonWriter writer, ProjectionDefinition value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("field");
        WriteField(writer, value.Field);
        if (value.Alias is null)
            writer.WriteNull("alias");
        else
            writer.WriteString("alias", value.Alias);
        writer.WriteEndObject();
    }

    internal static IFieldDto ParseField(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return FieldDefinitionParser.Parse(element.GetString()!);
        if (element.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(element, "kind", out var kindElement) ||
            kindElement.ValueKind != JsonValueKind.String)
            throw new JsonException("A projection expression requires a kind.");

        return kindElement.GetString()?.ToLowerInvariant() switch
        {
            "field" => new FieldDefinition(ReadString(element, "field")),
            "value" => new ConstFieldDefinition(ReadValue(element, "value")),
            "binary" => ParseBinary(element),
            "function" => ParseFunction(element),
            _ => throw new JsonException($"Invalid projection expression kind [{kindElement.GetString()}].")
        };
    }

    private static IFieldDto ParseBinary(JsonElement element)
    {
        var operation = ReadString(element, "operator");
        if (operation is not ("+" or "-" or "*" or "/"))
            throw new JsonException("A binary projection requires a supported arithmetic operator.");
        var arguments = ReadArguments(element);
        if (arguments.Length != 2)
            throw new JsonException("A binary projection requires exactly two arguments.");

        return new GroupFieldDefintion([arguments[0], new BinaryFieldDefinition(operation, arguments[1])]);
    }

    private static IFieldDto ParseFunction(JsonElement element)
        => new FunctionFieldDefinition(ReadString(element, "function"), ReadArguments(element));

    private static IFieldDto[] ReadArguments(JsonElement element)
    {
        if (!TryGetProperty(element, "arguments", out var arguments) ||
            arguments.ValueKind != JsonValueKind.Array)
            throw new JsonException("A projection function or binary expression requires an argument array.");
        return arguments.EnumerateArray().Select(ParseField).ToArray();
    }

    private static object? ReadValue(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
            throw new JsonException($"A projection {name} is required.");

        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => value.GetDecimal(),
            _ => throw new JsonException($"A projection {name} must be a scalar value.")
        };
    }

    private static string ReadString(JsonElement element, string name)
        => TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new JsonException($"A projection {name} must be a string.");

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value);

    internal static void WriteField(Utf8JsonWriter writer, IFieldDto field)
    {
        writer.WriteStartObject();
        WriteFieldExpression(writer, field);
        writer.WriteEndObject();
    }

    private static void WriteGroupExpression(Utf8JsonWriter writer, IFieldDto[] fields)
    {
        if (fields.Length == 0)
            throw new JsonException("A grouped projection cannot be empty.");

        if (fields.Length == 1)
        {
            WriteFieldExpression(writer, fields[0]);
            return;
        }

        var expression = fields[0];
        for (var index = 1; index < fields.Length; index++)
        {
            if (fields[index] is not BinaryFieldDefinition binary)
                throw new JsonException("Grouped projections must alternate fields and binary operators.");
            expression = new GroupFieldDefintion([expression, binary]);
        }

        WriteFieldExpression(writer, expression);
    }

    private static void WriteFieldExpression(Utf8JsonWriter writer, IFieldDto field)
    {
        if (field is GroupFieldDefintion { Fields.Length: 2 } group &&
            group.Fields[1] is BinaryFieldDefinition binary)
        {
            writer.WriteString("kind", "binary");
            writer.WriteString("operator", binary.Oper);
            writer.WritePropertyName("arguments");
            writer.WriteStartArray();
            WriteField(writer, group.Fields[0]);
            WriteField(writer, binary.Field);
            writer.WriteEndArray();
            return;
        }

        if (field is GroupFieldDefintion nested)
        {
            WriteGroupExpression(writer, nested.Fields);
            return;
        }

        switch (field)
        {
            case FieldDefinition fieldDto:
                writer.WriteString("kind", "field");
                writer.WriteString("field", fieldDto.FieldName);
                return;
            case ConstFieldDefinition constant:
                writer.WriteString("kind", "value");
                writer.WritePropertyName("value");
                JsonSerializer.Serialize(writer, constant.Value);
                return;
            case FunctionFieldDefinition function:
                writer.WriteString("kind", "function");
                writer.WriteString("function", function.FunctionName);
                writer.WritePropertyName("arguments");
                writer.WriteStartArray();
                foreach (var argument in function.Arguments)
                    WriteField(writer, argument);
                writer.WriteEndArray();
                return;
            default:
                throw new JsonException("Unsupported grouped projection expression.");
        }
    }

}
