using System.Linq.Expressions;
using System.Reflection;

namespace Namh.Data.DynamicSql;

public static class QueryDefinitionExtensions
{
    public static IDbQueryable Query(
        this DynamicSqlContext context,
        QueryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(definition);

        var query = context.Query(definition.Entity, definition.IncludeArchive);

        if (definition.Columns.Count > 0)
        {
            var selection = new DataRecord();
            foreach (var column in definition.Columns)
                selection[column.Alias ?? column.Field] = column.Field;

            query = query.Select(selection);
        }

        foreach (var filter in definition.Filters)
            query = query.Where(CreateFilter(filter));

        foreach (var sortGroup in definition.Sorts.Select((sort, index) => (sort, index)))
        {
            var fields = new[] { sortGroup.sort.Field };
            query = sortGroup.index == 0
                ? sortGroup.sort.Direction == QuerySortDirection.Ascending
                    ? query.OrderBy(fields)
                    : query.OrderByDescending(fields)
                : sortGroup.sort.Direction == QuerySortDirection.Ascending
                    ? query.ThenBy(fields)
                    : query.ThenByDescending(fields);
        }

        if (definition.Page is not null || definition.PageSize is not null)
        {
            var page = definition.Page ?? 1;
            var pageSize = definition.PageSize ?? 50;

            if (page < 1)
                throw new ArgumentOutOfRangeException(nameof(definition.Page), "Page must be at least 1.");

            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(definition.PageSize), "Page size must be at least 1.");

            query = query.Skip((page - 1) * pageSize);
            query = query.TakeQuery(pageSize);
        }

        return query;
    }

    private static Expression<Func<IRecordParameter, bool>> CreateFilter(QueryFilter filter)
    {
        var parameter = Expression.Parameter(typeof(IRecordParameter), "record");
        var field = Expression.Call(
            parameter,
            typeof(IRecordParameter).GetProperty("Item")!.GetMethod!,
            Expression.Constant(filter.Field));
        var value = Expression.Constant(filter.Value, typeof(object));

        var body = filter.Operator switch
        {
            QueryFilterOperator.Equals => Expression.Equal(field, value),
            QueryFilterOperator.NotEquals => Expression.NotEqual(field, value),
            QueryFilterOperator.GreaterThan => Expression.GreaterThan(field, value),
            QueryFilterOperator.LessThan => Expression.LessThan(field, value),
            QueryFilterOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(field, value),
            QueryFilterOperator.LessThanOrEqual => Expression.LessThanOrEqual(field, value),
            _ => throw new ArgumentOutOfRangeException(nameof(filter.Operator))
        };

        return Expression.Lambda<Func<IRecordParameter, bool>>(body, parameter);
    }
}
