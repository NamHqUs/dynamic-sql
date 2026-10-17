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
            EntityAliases = EntityContextDefinitionParser.Parse(definition.EntityAliases),
            Fields = ProjectionDefinitionParser.Parse(definition.Fields),
            Filter = FilterDefinitionParser.Parse(definition.Filter),
            Sorts = SortDefinitionParser.Parse(definition.Sorts),
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
    public static FilterDefinition? ParseFilter(string plainText) => FilterDefinitionParser.Parse(plainText);
    public static List<SortDefinition> ParseSorts(string plainText) => SortDefinitionParser.Parse(plainText);

    private record YamlQueryDefinition()
    {
        public string? Name { get; set; }
        public string? Entity { get; set; }
        public List<string?>? EntityAliases { get; set; }
        public List<string?>? Fields { get; set; }
        public string? Filter { get; set; }
        public string? Sorts { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
        public bool IncludeArchive { get; set; }
    }
}
