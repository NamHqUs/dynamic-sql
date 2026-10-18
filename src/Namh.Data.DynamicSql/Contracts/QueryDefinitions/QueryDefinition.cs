#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;

using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
#pragma warning restore IDE0130


public sealed class QueryDefinition
{
    public string? Name { get; init; }
    public required string Entity { get; init; }
    public List<EntityContextDefinition> EntityAliases { get; init; } = [];
    public List<ProjectionDefinition> Fields { get; init; } = [];
    public FilterDefinition? Filter { get; init; }
    public List<SortDefinition> Sorts { get; init; } = [];
    public int? Page { get; init; }
    public int? PageSize { get; init; }

    public static QueryDefinition FromYml(string yml)
        => QueryDefinitionParser.ParseFromYml(yml);

    public string ToYml()
    {
        List<string> lines = [];
        lines.Add($"Name: {Name}");
        lines.Add($"Entity: {Entity}");

        if (EntityAliases.Count > 0)
        {
            lines.Add("EntityAliases:");
            lines.AddRange(EntityAliases.Select(alias =>
                $"  - \"{alias.Alias}: {alias.RelationPath}({string.Join(", ", alias.RelatedKeys.Select(key => $"{key.FromDbColumn}:{key.ToDbColumn}"))})\""));
        }

        if (Fields.Count > 0)
        {
            lines.Add("Fields:");
            lines.AddRange(Fields.Select(field => $"  - {field.ToText()}"));
        }

        if (Filter is not null)
        {
            lines.Add($"Filter: {Filter.ToText()}");
        }

        if (Sorts.Count > 0)
        {
            lines.Add($"Sorts: {string.Join(", ", Sorts.Select(FormatSort))}");
        }

        if (Page is not null)
            lines.Add($"Page: {Page}");

        if (PageSize is not null)
            lines.Add($"PageSize: {PageSize}");

        return string.Join(Environment.NewLine, lines.Where(line => line is not null)!);
    }

    public override string ToString()
        => ToYml();

    private static string FormatSort(SortDefinition sort)
        => $"{sort.Field.ToText()} {sort.Direction switch
        {
            QuerySortDirection.Ascending => "ASC",
            QuerySortDirection.Descending => "DESC",
            _ => throw new ArgumentOutOfRangeException(nameof(sort.Direction))
        }}";

}
