using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

[JsonConverter(typeof(FieldJsonConverter))]
public interface IField
{
    string Name { get; init; }
}

public sealed record DbField(string Name, string? DbColumn = null) : IField;

public sealed record RawSqlField(string Name, string Expression) : IField;

public enum DataType
{
    String = 0,
    Number,
    Datetime,
    Boolean,
}

public sealed class FieldJsonConverter : JsonConverter<IField>
{
    public override IField Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var name = GetString(root, "name")
            ?? throw new JsonException("Field name is required.");
        var kind = GetString(root, "kind");

        if (kind is not null)
        {
            return kind switch
            {
                "dbField" => new DbField(name, GetString(root, "dbColumn")),
                "rawSqlField" => new RawSqlField(
                    name,
                    GetString(root, "expression")
                        ?? throw new JsonException("Raw SQL field expression is required.")),
                _ => throw new JsonException($"Unsupported field kind [{kind}].")
            };
        }

        return IsLegacyRawSql(root)
            ? new RawSqlField(name, GetString(root, "rawSql") ?? name)
            : new DbField(name, GetString(root, "rawSql") ?? name);
    }

    public override void Write(Utf8JsonWriter writer, IField value, JsonSerializerOptions options)
    {
        var nameProperty = options.PropertyNamingPolicy?.ConvertName(nameof(IField.Name)) ?? nameof(IField.Name);
        writer.WriteStartObject();

        switch (value)
        {
            case DbField dbField:
                writer.WriteString("kind", "dbField");
                writer.WriteString(nameProperty, dbField.Name);
                writer.WriteString(
                    options.PropertyNamingPolicy?.ConvertName(nameof(DbField.DbColumn)) ?? nameof(DbField.DbColumn),
                    dbField.DbColumn);
                break;
            case RawSqlField rawSqlField:
                writer.WriteString("kind", "rawSqlField");
                writer.WriteString(nameProperty, rawSqlField.Name);
                writer.WriteString(
                    options.PropertyNamingPolicy?.ConvertName(nameof(RawSqlField.Expression)) ?? nameof(RawSqlField.Expression),
                    rawSqlField.Expression);
                break;
            default:
                throw new JsonException($"Unsupported field type [{value.GetType().Name}].");
        }

        writer.WriteEndObject();
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind == JsonValueKind.Null)
                    return null;
                if (property.Value.ValueKind != JsonValueKind.String)
                    throw new JsonException($"Field property [{propertyName}] must be a string.");
                return property.Value.GetString();
            }
        }

        return null;
    }

    private static bool IsLegacyRawSql(JsonElement element)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, "FieldType", StringComparison.OrdinalIgnoreCase))
                continue;

            if (property.Value.ValueKind == JsonValueKind.String)
            {
                var legacyKind = property.Value.GetString();
                if (string.Equals(legacyKind, "RawSql", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (string.Equals(legacyKind, "DbColumn", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else if (property.Value.ValueKind == JsonValueKind.Number)
            {
                var legacyKind = property.Value.GetInt32();
                if (legacyKind == 1)
                    return true;
                if (legacyKind == 0)
                    return false;
            }

            throw new JsonException("Unsupported legacy field type.");
        }

        return false;
    }
}
