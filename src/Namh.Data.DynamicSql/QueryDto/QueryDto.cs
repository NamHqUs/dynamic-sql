#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public sealed class QueryDto
{
    public string? Name { get; init; }
    public required string Entity { get; init; }
    public List<ProjectionField> Fields { get; init; } = [];
    public List<string> GroupBy { get; init; } = [];
    public FilterDto? Filter { get; init; }
    public List<SortDto> Sorts { get; init; } = [];
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public bool IncludeArchive { get; init; }

    public static QueryDto FromJson(string json)
        => QueryDtoParsing.QueryDtoParser.Parse(json);

    public override string ToString()
        => string.Join(Environment.NewLine,
            new[]
            {
                Name is null ? null : $"Name: {Name}",
                $"Entity: {Entity}",
                $"Fields: {string.Join(", ", Fields.Select(field => field.ToText()))}",
                GroupBy.Count == 0 ? null : $"GroupBy: {string.Join(", ", GroupBy)}",
                $"Filters: {Filter?.ToText() ?? string.Empty}",
                $"Sorts: {string.Join(", ", Sorts.Select(FormatSort))}",
                $"Page: {Page}",
                $"PageSize: {PageSize}",
                $"IncludeArchive: {IncludeArchive}"
            }.Where(line => line is not null)!);

    private static string FormatSort(SortDto sort)
        => $"{sort.Field} {sort.Direction switch
        {
            QuerySortDirection.Ascending => "ASC",
            QuerySortDirection.Descending => "DESC",
            _ => throw new ArgumentOutOfRangeException(nameof(sort.Direction))
        }}";

}
