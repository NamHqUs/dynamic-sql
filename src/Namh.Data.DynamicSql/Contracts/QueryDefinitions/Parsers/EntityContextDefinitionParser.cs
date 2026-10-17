using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

namespace Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

internal static class EntityContextDefinitionParser
{
    public static List<EntityContextDefinition> Parse(List<string?>? texts)
        => texts?.Select(text => !string.IsNullOrWhiteSpace(text)
            ? Parse(text)
            : throw new FormatException("A YAML entity alias cannot be empty.")).ToList() ?? [];

    private static EntityContextDefinition Parse(string text)
    {
        var aliasSeparator = text.IndexOf(':');
        if (aliasSeparator <= 0)
            throw new FormatException($"YAML entity alias [{text}] requires an alias name.");

        var alias = text[..aliasSeparator].Trim();
        var relation = text[(aliasSeparator + 1)..].Trim();
        var opening = relation.IndexOf('(');
        if (opening <= 0 || relation[^1] != ')')
            throw new FormatException($"YAML entity alias [{alias}] requires a relation path and join keys.");

        var relationPath = relation[..opening].Trim();
        if (relationPath.Length == 0)
            throw new FormatException($"YAML entity alias [{alias}] requires a relation path.");
        if (!HasClosingParenthesisAtEnd(relation, opening))
            throw new FormatException($"YAML entity alias [{alias}] contains an invalid relation key list.");

        var keys = FieldDefinitionParser.SplitTopLevel(relation[(opening + 1)..^1], ',')
            .Select(key => ParseKey(alias, key))
            .ToList();
        if (keys.Count == 0)
            throw new FormatException($"YAML entity alias [{alias}] requires at least one join key.");

        return new EntityContextDefinition(alias, relationPath, keys);
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

    private static RelationItem ParseKey(string alias, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException($"YAML entity alias [{alias}] contains an empty join key.");

        var mappingSeparator = text.IndexOf(':');
        if (mappingSeparator < 0)
            return new RelationItem(text.Trim());

        var fromColumn = text[..mappingSeparator].Trim();
        var toColumn = text[(mappingSeparator + 1)..].Trim();
        if (fromColumn.Length == 0 || toColumn.Length == 0)
            throw new FormatException($"YAML entity alias [{alias}] contains an invalid join key mapping.");

        return new RelationItem(fromColumn, toColumn);
    }
}
