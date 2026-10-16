using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

internal class ProjectionDtoParser
{
    public static List<ProjectionDto> Parse(string text)
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

    static ProjectionDto FromText(string text)
    {
        var separatorIndex = text.IndexOf('|');
        var fieldText = separatorIndex < 0 ? text.Trim() : text[..separatorIndex].Trim();
        var alias = separatorIndex < 0 ? null : text[(separatorIndex + 1)..].Trim();
        return new ProjectionDto(FieldDtoParser.Parse(fieldText), alias);
    }
}
