#pragma warning disable IDE0130
using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;
using System.Text.Json.Serialization;

namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

[JsonConverter(typeof(ProjectionDtoJsonConverter))]
public sealed record ProjectionDto(IFieldDto Field, string? Alias = null)
{
    public string ToText() => Alias is null ? Field.ToText() : $"{Field.ToText()} | {Alias}";
}
