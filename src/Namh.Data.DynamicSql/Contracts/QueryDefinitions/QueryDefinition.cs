#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;

using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
#pragma warning restore IDE0130


public sealed class QueryDefinition
{
    public string? Name { get; init; }
    public required string Entity { get; init; }
    public List<ProjectionDefinition> Fields { get; init; } = [];
    public FilterDefinition? Filter { get; init; }
    public List<SortDefinition> Sorts { get; init; } = [];
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public bool IncludeArchive { get; init; }

    public static QueryDefinition FromJson(string json)
        => QueryDefinitionParser.Parse(json);

    public override string ToString()
        => string.Join(Environment.NewLine,
            new[]
            {
                Name is null ? null : $"Name: {Name}",
                $"Entity: {Entity}",
                $"Fields: {string.Join(", ", Fields.Select(field => field.ToText()))}",
                $"Filters: {Filter?.ToText() ?? string.Empty}",
                $"Sorts: {string.Join(", ", Sorts.Select(FormatSort))}",
                $"Page: {Page}",
                $"PageSize: {PageSize}",
                $"IncludeArchive: {IncludeArchive}"
            }.Where(line => line is not null)!);

    private static string FormatSort(SortDefinition sort)
        => $"{sort.Field.ToText()} {sort.Direction switch
        {
            QuerySortDirection.Ascending => "ASC",
            QuerySortDirection.Descending => "DESC",
            _ => throw new ArgumentOutOfRangeException(nameof(sort.Direction))
        }}";

}
