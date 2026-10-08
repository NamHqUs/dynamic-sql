namespace Namh.Data.DynamicSql;

public sealed class QueryDefinition
{
    public required string Entity { get; init; }
    public List<QueryColumn> Columns { get; init; } = [];
    public List<QueryFilter> Filters { get; init; } = [];
    public List<QuerySort> Sorts { get; init; } = [];
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public bool IncludeArchive { get; init; }
}

public sealed record QueryColumn(string Field, string? Alias = null);

public sealed record QueryFilter(
    string Field,
    QueryFilterOperator Operator,
    object? Value = null);

public enum QueryFilterOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual
}

public sealed record QuerySort(
    string Field,
    QuerySortDirection Direction = QuerySortDirection.Ascending);

public enum QuerySortDirection
{
    Ascending,
    Descending
}
