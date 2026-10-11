using Namh.Data.DynamicSql.QueryRequestParsing;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public sealed class QueryRequest
{
    public required string Entity { get; init; }
    public List<QueryColumn> Columns { get; init; } = [];
    public QueryFilter? Filter { get; init; }
    public List<QuerySort> Sorts { get; init; } = [];
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public bool IncludeArchive { get; init; }

    public static QueryRequest FromJson(string json)
        => QueryRequestJsonParser.Parse(json);

    public override string ToString()
        => string.Join(Environment.NewLine,
            $"Entity: {Entity}",
            $"Columns: {(Columns.Count == 0 ? "Empty" : string.Join(", ", Columns.Select(FormatColumn)))}",
            $"Filters: {(Filter is null || !Filter.Enabled ? "Empty" : FormatFilter(Filter, false))}",
            $"Sorts: {(Sorts.Count == 0 ? "Empty" : string.Join(", ", Sorts.Select(FormatSort)))}",
            $"Page: {Page?.ToString() ?? "Empty"}",
            $"PageSize: {PageSize?.ToString() ?? "Empty"}",
            $"IncludeArchive: {IncludeArchive}");

    private static string FormatColumn(QueryColumn column)
        => column.Alias is null ? column.Field : $"{column.Field} AS {column.Alias}";

    private static string FormatSort(QuerySort sort)
        => $"{sort.Field} {sort.Direction switch
        {
            QuerySortDirection.Ascending => "ASC",
            QuerySortDirection.Descending => "DESC",
            _ => throw new ArgumentOutOfRangeException(nameof(sort.Direction))
        }}";

    private static string FormatFilter(QueryFilter filter, bool parenthesize)
    {
        var text = filter switch
        {
            LogicalFilter logical =>
                $"{FormatFilter(logical.Left, true)} {FormatLogicalOperator(logical.Operator)} " +
                $"{FormatFilter(logical.Right, true)}",
            BinaryFilter binary =>
                $"{binary.Field} {FormatFilterOperator(binary.Operator)} {FormatValue(binary.Value)}",
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };

        return parenthesize && filter is LogicalFilter ? $"({text})" : text;
    }

    private static string FormatLogicalOperator(QueryFilterLogicalOperator @operator)
        => @operator switch
        {
            QueryFilterLogicalOperator.And => "AND",
            QueryFilterLogicalOperator.Or => "OR",
            _ => throw new ArgumentOutOfRangeException(nameof(@operator))
        };

    private static string FormatFilterOperator(QueryFilterOperator @operator)
        => @operator switch
        {
            QueryFilterOperator.Equals => "=",
            QueryFilterOperator.NotEquals => "<>",
            QueryFilterOperator.Like => "LIKE",
            QueryFilterOperator.GreaterThan => ">",
            QueryFilterOperator.LessThan => "<",
            QueryFilterOperator.GreaterThanOrEqual => ">=",
            QueryFilterOperator.LessThanOrEqual => "<=",
            _ => throw new ArgumentOutOfRangeException(nameof(@operator))
        };

    private static string FormatValue(object? value)
        => value switch
        {
            null => "NULL",
            string text => $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"",
            QueryFilterArithmetic arithmetic =>
                $"({FormatValue(arithmetic.Left)} {arithmetic.Operator} {arithmetic.Field})",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "NULL"
        };
}
