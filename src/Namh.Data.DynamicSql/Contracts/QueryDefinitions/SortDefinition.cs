#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public enum QuerySortDirection
{
    Ascending,
    Descending
}

public sealed record SortDefinition(
    IFieldDto Field,
    QuerySortDirection Direction = QuerySortDirection.Ascending);