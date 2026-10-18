using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

namespace Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

internal static class EntityContextDefinitionParser
{
    public static List<EntityContextDefinition> Parse(IEnumerable<string> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return definitions.Select(Parse).ToList();
    }

    private static EntityContextDefinition Parse(string definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition);

        var separator = definition.IndexOf(':');
        if (separator < 1)
            throw new FormatException($"Invalid entity alias definition [{definition}].");

        var alias = definition[..separator].Trim();
        var relation = definition[(separator + 1)..].Trim();
        var keyStart = relation.LastIndexOf('(');
        if (keyStart < 1 || !relation.EndsWith(')'))
            throw new FormatException($"Entity alias [{alias}] requires relation key mappings.");

        var relationPath = relation[..keyStart].Trim();
        var relatedKeys = FieldDefinitionParser.SplitTopLevel(relation[(keyStart + 1)..^1], ',')
            .Select(ParseRelatedKey)
            .ToList();

        if (string.IsNullOrWhiteSpace(alias) ||
            string.IsNullOrWhiteSpace(relationPath) ||
            relatedKeys.Count == 0)
            throw new FormatException($"Invalid entity alias definition [{definition}].");

        return new EntityContextDefinition(alias, relationPath, relatedKeys);
    }

    private static RelationItem ParseRelatedKey(string keyText)
    {
        var separator = keyText.IndexOf(':');
        var fromColumn = separator < 0 ? keyText.Trim() : keyText[..separator].Trim();
        var toColumn = separator < 0 ? fromColumn : keyText[(separator + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(fromColumn) || string.IsNullOrWhiteSpace(toColumn))
            throw new FormatException($"Invalid relation key mapping [{keyText}].");

        return new RelationItem(fromColumn, toColumn);
    }
}
