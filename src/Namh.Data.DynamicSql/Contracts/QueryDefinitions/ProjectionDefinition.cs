#pragma warning disable IDE0130
using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
using System.Text.Json.Serialization;

namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

[JsonConverter(typeof(ProjectionDefinitionJsonConverter))]
public sealed record ProjectionDefinition(IFieldDto Field, string? Alias = null)
{
    public string ToText() => Alias is null ? Field.ToText() : $"{Field.ToText()} | {Alias}";
}
