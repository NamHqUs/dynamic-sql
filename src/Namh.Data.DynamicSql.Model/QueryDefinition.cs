namespace Namh.Data.DynamicSql;

public sealed class QueryDefinition
{
    public required string Entity { get; init; }
    public List<QueryColumn> Columns { get; init; } = [];
    public List<QueryFilter> Filters { get; init; } = [];
    public QueryFilterExpression? Filter { get; init; }
    public List<QuerySort> Sorts { get; init; } = [];
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public bool IncludeArchive { get; init; }

    public static QueryDefinition FromJson(string json)
        => QueryDefinitionJsonParser.Parse(json);
}

public sealed record QueryColumn(string Field, string? Alias = null);

public sealed record QueryFilter(
    string Field,
    QueryFilterOperator Operator,
    object? Value = null,
    bool Parameterized = false);

public sealed record QueryFilterArithmetic(
    object? Left,
    string Operator,
    string Field);

public enum QueryFilterOperator
{
    Equals,
    NotEquals,
    Like,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual
}

public abstract record QueryFilterExpression;

public sealed record QueryFilterCondition(QueryFilter Filter) : QueryFilterExpression;

public sealed record QueryFilterGroup(
    QueryFilterLogicalOperator Operator,
    IReadOnlyList<QueryFilterExpression> Conditions) : QueryFilterExpression;

public enum QueryFilterLogicalOperator
{
    And,
    Or
}

public sealed record QuerySort(
    string Field,
    QuerySortDirection Direction = QuerySortDirection.Ascending);

public enum QuerySortDirection
{
    Ascending,
    Descending
}
