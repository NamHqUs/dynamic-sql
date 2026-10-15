#pragma warning disable IDE0130
using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;
using System.Text.Json.Serialization;

namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

[JsonConverter(typeof(ProjectionDtoJsonConverter))]
public sealed record ProjectionDto(IFieldDto Field, string? Alias = null)
{
    public string ToText() => Alias is null ? Field.ToText() : $"{Field.ToText()} | {Alias}";
    public static ProjectionDto FromText(string text)
    {
        var separatorIndex = text.IndexOf('|');
        var fieldText = separatorIndex < 0 ? text.Trim() : text[..separatorIndex].Trim();
        var alias = separatorIndex < 0 ? null : text[(separatorIndex + 1)..].Trim();
        return new ProjectionDto(FieldDtoParser.Parse(fieldText), alias);
    }

    public static List<ProjectionDto> FromTextList(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return FieldDtoParser.SplitTopLevel(text, ',')
            .Select(item =>
            {
                var aliasSeparator = item.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
                return aliasSeparator < 0
                    ? FromText(item)
                    : FromText($"{item[..aliasSeparator].Trim()} | {item[(aliasSeparator + 4)..].Trim()}");
            })
            .ToList();
    }
}
