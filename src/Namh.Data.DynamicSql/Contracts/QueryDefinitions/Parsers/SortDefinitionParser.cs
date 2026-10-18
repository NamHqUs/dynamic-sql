using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

namespace Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

internal static class SortDefinitionParser
{
    public static List<SortDefinition> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return FieldDefinitionParser.SplitTopLevel(text, ',')
            .Where(sort => !string.IsNullOrWhiteSpace(sort))
            .Select(ParseSort)
            .ToList();
    }

    private static SortDefinition ParseSort(string text)
    {
        var sortText = text.Trim();
        var direction = QuerySortDirection.Ascending;
        if (sortText.EndsWith(" ASC", StringComparison.OrdinalIgnoreCase))
        {
            sortText = sortText[..^4].TrimEnd();
        }
        else if (sortText.EndsWith(" DESC", StringComparison.OrdinalIgnoreCase))
        {
            sortText = sortText[..^5].TrimEnd();
            direction = QuerySortDirection.Descending;
        }

        if (string.IsNullOrWhiteSpace(sortText))
            throw new FormatException($"Invalid sort definition [{text}].");

        return new SortDefinition(FieldDefinitionParser.Parse(sortText), direction);
    }
}
