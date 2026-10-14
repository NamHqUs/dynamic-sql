using Namh.Data.DynamicSql.QueryRequestParsing;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public sealed class QueryRequest
{
    public string? Name { get; init; }
    public required string Entity { get; init; }
    public List<FieldTranslator> Columns { get; init; } = [];
    public List<string> GroupBy { get; init; } = [];
    public QueryFilter? Filter { get; init; }
    public List<QuerySort> Sorts { get; init; } = [];
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public bool IncludeArchive { get; init; }

    public static QueryRequest FromJson(string json)
        => QueryRequestJsonParser.Parse(json);

    public override string ToString()
        => string.Join(Environment.NewLine,
            new[]
            {
                Name is null ? null : $"Name: {Name}",
                $"Entity: {Entity}",
                $"Columns: {(Columns.Any(column => column.Expression is not null)
                    ? System.Text.Json.JsonSerializer.Serialize(Columns, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
                    : string.Join(", ", Columns.Select(FormatColumn)))}",
                GroupBy.Count == 0 ? null : $"GroupBy: {string.Join(", ", GroupBy)}",
                $"Filters: {(Filter is null ? string.Empty : FormatFilter(Filter, false))}",
                $"Sorts: {string.Join(", ", Sorts.Select(FormatSort))}",
                $"Page: {Page}",
                $"PageSize: {PageSize}",
                $"IncludeArchive: {IncludeArchive}"
            }.Where(line => line is not null)!);

    private static string FormatColumn(FieldTranslator column)
        => column.Alias is null ? column.Field ?? string.Empty : $"{column.Field} AS {column.Alias}";

    private static string FormatSort(QuerySort sort)
        => $"{sort.Field} {sort.Direction switch
        {
            QuerySortDirection.Ascending => "ASC",
            QuerySortDirection.Descending => "DESC",
            _ => throw new ArgumentOutOfRangeException(nameof(sort.Direction))
        }}";

    private static string FormatFilter(IQueryFilter filter, bool parenthesize)
    {
        var text = filter switch
        {
            QueryFilter group => string.Join(
                $" {FormatLogicalOperator(group.LogicalOperation)} ",
                group.Filters.Select(item => FormatFilter(item, item is QueryFilter))),
            QueryFilterItem binary =>
                $"{binary.Field} {FormatFilterOperator(binary.Operation)} {FormatValue(binary.Value)}",
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };

        return parenthesize && filter is QueryFilter ? $"({text})" : text;
    }

    private static string FormatLogicalOperator(LogicalOperator @operator)
        => @operator switch
        {
            LogicalOperator.And => "AND",
            LogicalOperator.Or => "OR",
            _ => throw new ArgumentOutOfRangeException(nameof(@operator))
        };

    private static string FormatFilterOperator(BinaryOperator @operator)
        => @operator switch
        {
            BinaryOperator.Equals => "=",
            BinaryOperator.NotEquals => "<>",
            BinaryOperator.Like => "LIKE",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.LessThan => "<",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.LessThanOrEqual => "<=",
            _ => throw new ArgumentOutOfRangeException(nameof(@operator))
        };

    private static string FormatValue(object? value)
        => value switch
        {
            null => "NULL",
            string text => $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "NULL"
        };
}
