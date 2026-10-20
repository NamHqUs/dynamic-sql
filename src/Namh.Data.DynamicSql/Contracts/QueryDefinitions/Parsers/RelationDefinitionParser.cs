using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

namespace Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

internal static class RelationDefinitionParser
{
    public static List<RelationDefinition> Parse(List<string?>? texts)
        => texts?.Select(text => !string.IsNullOrWhiteSpace(text)
            ? Parse(text)
            : throw new FormatException("A YAML relation cannot be empty.")).ToList() ?? [];

    private static RelationDefinition Parse(string text)
    {
        var nameSeparator = text.IndexOf(':');
        if (nameSeparator <= 0)
            throw new FormatException($"YAML relation [{text}] requires a name.");

        var name = text[..nameSeparator].Trim();
        var relation = text[(nameSeparator + 1)..].Trim();
        var opening = relation.IndexOf('(');
        if (opening <= 0 || relation[^1] != ')')
            throw new FormatException($"YAML relation [{name}] requires a relation path and join keys.");

        var relationPath = relation[..opening].Trim();
        if (relationPath.Length == 0)
            throw new FormatException($"YAML relation [{name}] requires a relation path.");
        if (!HasClosingParenthesisAtEnd(relation, opening))
            throw new FormatException($"YAML relation [{name}] contains an invalid relation key list.");

        var keys = FieldDefinitionParser.SplitTopLevel(relation[(opening + 1)..^1], ',')
            .Select(key => ParseKey(name, key))
            .ToList();
        if (keys.Count == 0)
            throw new FormatException($"YAML relation [{name}] requires at least one join key.");

        return new RelationDefinition(name, relationPath, keys);
    }

    private static bool HasClosingParenthesisAtEnd(string relation, int opening)
    {
        var depth = 0;
        for (var index = opening; index < relation.Length; index++)
        {
            if (relation[index] == '(')
                depth++;
            else if (relation[index] == ')' && --depth == 0)
                return index == relation.Length - 1;
        }
        return false;
    }

    private static RelationItem ParseKey(string name, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException($"YAML relation [{name}] contains an empty join key.");

        var mappingSeparator = text.IndexOf(':');
        if (mappingSeparator < 0)
            return new RelationItem(text.Trim());

        var fromColumn = text[..mappingSeparator].Trim();
        var toColumn = text[(mappingSeparator + 1)..].Trim();
        if (fromColumn.Length == 0 || toColumn.Length == 0)
            throw new FormatException($"YAML relation [{name}] contains an invalid join key mapping.");

        return new RelationItem(fromColumn, toColumn);
    }
}
