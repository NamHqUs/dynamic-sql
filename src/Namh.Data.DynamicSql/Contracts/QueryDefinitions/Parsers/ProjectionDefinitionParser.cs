using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

namespace Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

internal class ProjectionDefinitionParser
{
    public static List<ProjectionDefinition> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return FieldDefinitionParser.SplitTopLevel(text, ',')
            .Select(item =>
            {
                var aliasSeparator = item.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
                return aliasSeparator < 0
                    ? FromText(item)
                    : FromText($"{item[..aliasSeparator].Trim()} | {item[(aliasSeparator + 4)..].Trim()}");
            })
            .ToList();
    }

    static ProjectionDefinition FromText(string text)
    {
        var separatorIndex = text.IndexOf('|');
        var fieldText = separatorIndex < 0 ? text.Trim() : text[..separatorIndex].Trim();
        var alias = separatorIndex < 0 ? null : text[(separatorIndex + 1)..].Trim();
        return new ProjectionDefinition(FieldDefinitionParser.Parse(fieldText), alias);
    }
}
