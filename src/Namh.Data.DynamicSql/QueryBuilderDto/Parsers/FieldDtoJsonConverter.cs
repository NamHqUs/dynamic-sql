using System.Text.Json;
using System.Text.Json.Serialization;

namespace Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

internal sealed class FieldDtoJsonConverter : JsonConverter<IFieldDto>
{
    public override IFieldDto Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new FieldDto(reader.GetString()
                ?? throw new JsonException("A filter field cannot be null."));

        using var document = JsonDocument.ParseValue(ref reader);
        return ProjectionDtoJsonConverter.ParseField(document.RootElement);
    }

    public override void Write(
        Utf8JsonWriter writer,
        IFieldDto value,
        JsonSerializerOptions options)
        => ProjectionDtoJsonConverter.WriteField(writer, value);
}
