using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;
using YamlDotNet.Serialization;

namespace Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

public static class QueryDefinitionParser
{
    public static QueryDefinition ParseFromYml(string yml)
    {
        ArgumentNullException.ThrowIfNull(yml);

        var definition = new DeserializerBuilder()
            .Build()
            .Deserialize<YamlQueryDefinition>(yml)
            ?? throw new FormatException("The YAML query definition cannot be null.");

        return new QueryDefinition
        {
            Name = definition.Name,
            Entity = !string.IsNullOrWhiteSpace(definition.Entity)
                ? definition.Entity
                : throw new FormatException("The YAML query definition requires an entity."),
            EntityAliases = (definition.EntityAliases ?? [])
                .Select(ParseYamlEntityAlias)
                .ToList(),
            Fields = (definition.Fields ?? [])
                .SelectMany(field => ParseProjections(
                    field ?? throw new FormatException("A YAML projection cannot be null.")))
                .ToList(),
            Filter = definition.Filter is null
                ? null
                : FilterDefinitionParser.Parse(definition.Filter),
            Sorts = (definition.Sorts ?? [])
                .Select(ParseYamlSort)
                .ToList(),
            Page = definition.Page,
            PageSize = definition.PageSize,
            IncludeArchive = definition.IncludeArchive
        };
    }

    public static QueryDefinition ParseFromYml(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return ParseFromYml(File.ReadAllText(file.FullName));
    }

    public static IFieldDto ParseField(string plainText) => FieldDefinitionParser.Parse(plainText);
    public static List<ProjectionDefinition> ParseProjections(string plainText) => ProjectionDefinitionParser.Parse(plainText);
    public static FilterDefinition ParseFilter(string plainText) => FilterDefinitionParser.Parse(plainText);

    private static EntityContextDefinition ParseYamlEntityAlias(YamlEntityContextDefinition alias)
    {
        var name = !string.IsNullOrWhiteSpace(alias.Name)
            ? alias.Name
            : throw new FormatException("A YAML entity alias requires a name.");
        var entity = !string.IsNullOrWhiteSpace(alias.Entity)
            ? alias.Entity
            : throw new FormatException($"YAML entity alias [{name}] requires an entity.");
        var relatedKeys = (alias.JoinKeys ?? [])
            .Select(key => new RelationItem(
                !string.IsNullOrWhiteSpace(key)
                    ? key
                    : throw new FormatException($"YAML entity alias [{name}] contains an empty join key.")))
            .ToList();

        if (relatedKeys.Count == 0)
            throw new FormatException($"YAML entity alias [{name}] requires at least one join key.");

        return new EntityContextDefinition(name, entity, relatedKeys);
    }

    private static SortDefinition ParseYamlSort(YamlSortDefinition sort)
    {
        var field = !string.IsNullOrWhiteSpace(sort.Field)
            ? FieldDefinitionParser.Parse(sort.Field)
            : throw new FormatException("A YAML sort requires a field expression.");
        if (string.IsNullOrWhiteSpace(sort.Direction))
            return new SortDefinition(field);
        if (!Enum.TryParse<QuerySortDirection>(sort.Direction, true, out var direction) ||
            !Enum.IsDefined(direction))
            throw new FormatException($"YAML sort direction [{sort.Direction}] is invalid.");

        return new SortDefinition(field, direction);
    }

    private sealed class YamlQueryDefinition
    {
        public string? Name { get; set; }
        public string? Entity { get; set; }
        public List<YamlEntityContextDefinition>? EntityAliases { get; set; }
        public List<string?>? Fields { get; set; }
        public string? Filter { get; set; }
        public List<YamlSortDefinition>? Sorts { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
        public bool IncludeArchive { get; set; }
    }

    private sealed class YamlEntityContextDefinition
    {
        public string? Name { get; set; }
        public string? Entity { get; set; }
        public List<string?>? JoinKeys { get; set; }
    }

    private sealed class YamlSortDefinition
    {
        public string? Field { get; set; }
        public string? Direction { get; set; }
    }
}
