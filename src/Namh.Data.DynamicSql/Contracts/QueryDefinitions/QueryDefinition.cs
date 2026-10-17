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
    public bool IncludeArchive { get; init; }

    public static QueryDefinition FromJson(string json)
        => QueryDefinitionParser.Parse(json);

    public override string ToString()
    {
        var lines = new List<string?>
        {
            Name is null ? null : $"Name: {Name}"
        };

        if (EntityAliases.Count == 0)
        {
            lines.Add($"Entity: {Entity}");
        }
        else
        {
            lines.Add("Entities:");
            lines.Add($"  {Entity}");
            lines.AddRange(EntityAliases.Select(alias =>
                $"  {alias.Alias}: {alias.RelationPath}({string.Join(", ", alias.RelatedKeys.Select(key => $"{key.FromDbColumn}:{key.ToDbColumn}"))})"));
        }

        lines.AddRange([
            $"Fields: {string.Join(", ", Fields.Select(field => field.ToText()))}",
            $"Filters: {Filter?.ToText() ?? string.Empty}",
            $"Sorts: {string.Join(", ", Sorts.Select(FormatSort))}",
            $"Page: {Page}",
            $"PageSize: {PageSize}",
            $"IncludeArchive: {IncludeArchive}"
        ]);

        return string.Join(Environment.NewLine, lines.Where(line => line is not null)!);
    }

    private static string FormatSort(SortDefinition sort)
        => $"{sort.Field.ToText()} {sort.Direction switch
        {
            QuerySortDirection.Ascending => "ASC",
            QuerySortDirection.Descending => "DESC",
            _ => throw new ArgumentOutOfRangeException(nameof(sort.Direction))
        }}";

}
