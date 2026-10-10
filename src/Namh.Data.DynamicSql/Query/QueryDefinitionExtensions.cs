using System.Linq.Expressions;
using System.Text.Json;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

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

        if (definition.Filter is not null)
            query = query.Where(CreateFilter(definition.Filter));

        foreach (var filter in definition.Filters)
            query = query.Where(CreateFilter(new QueryFilterCondition(filter)));

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

    private static Expression<Func<IRecordParameter, bool>> CreateFilter(QueryFilterExpression filter)
    {
        var parameter = Expression.Parameter(typeof(IRecordParameter), "record");
        var body = CreateFilterBody(filter, parameter);

        return Expression.Lambda<Func<IRecordParameter, bool>>(body, parameter);
    }

    private static Expression CreateFilterBody(
        QueryFilterExpression filter,
        ParameterExpression parameter)
        => filter switch
        {
            QueryFilterCondition condition => CreateCondition(condition.Filter, parameter),
            QueryFilterGroup group => CreateGroup(group, parameter),
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };

    private static Expression CreateGroup(
        QueryFilterGroup group,
        ParameterExpression parameter)
    {
        var expressions = group.Conditions
            .Select(condition => CreateFilterBody(condition, parameter))
            .ToList();
        var result = expressions[0];

        foreach (var expression in expressions.Skip(1))
            result = group.Operator == QueryFilterLogicalOperator.And
                ? Expression.AndAlso(result, expression)
                : Expression.OrElse(result, expression);

        return result;
    }

    private static Expression CreateCondition(
        QueryFilter filter,
        ParameterExpression parameter)
    {
        var field = Expression.Call(
            parameter,
            typeof(IRecordParameter).GetProperty("Item")!.GetMethod!,
            Expression.Constant(filter.Field));

        var filterValue = NormalizeFilterValue(filter.Value);

        if (filter.Operator == QueryFilterOperator.Like)
            return Expression.Call(
                field,
                typeof(DataValue).GetMethod(nameof(DataValue.Like))!,
                Expression.Constant(filterValue, typeof(object)));

        var value = CreateFilterValue(filter, parameter);

        return filter.Operator switch
        {
            QueryFilterOperator.Equals => Expression.Equal(field, value),
            QueryFilterOperator.NotEquals => Expression.NotEqual(field, value),
            QueryFilterOperator.GreaterThan => Expression.GreaterThan(field, value),
            QueryFilterOperator.LessThan => Expression.LessThan(field, value),
            QueryFilterOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(field, value),
            QueryFilterOperator.LessThanOrEqual => Expression.LessThanOrEqual(field, value),
            _ => throw new ArgumentOutOfRangeException(nameof(filter.Operator))
        };
    }

    private static Expression CreateFilterValue(
        QueryFilter filter,
        ParameterExpression parameter)
    {
        if (filter.Value is QueryFilterArithmetic arithmetic)
        {
            var left = Expression.Call(
                typeof(DataValue).GetMethod(nameof(DataValue.Parameter))!,
                Expression.Constant(arithmetic.Left, typeof(object)));
            var right = Expression.Call(
                parameter,
                typeof(IRecordParameter).GetProperty("Item")!.GetMethod!,
                Expression.Constant(arithmetic.Field));

            return arithmetic.Operator switch
            {
                "+" => Expression.Add(left, right),
                "-" => Expression.Subtract(left, right),
                "*" => Expression.Multiply(left, right),
                "/" => Expression.Divide(left, right),
                _ => throw new FormatException(
                    $"Unsupported arithmetic operator [{arithmetic.Operator}].")
            };
        }

        var value = NormalizeFilterValue(filter.Value);

        return filter.Parameterized
            ? Expression.Call(
                typeof(DataValue).GetMethod(nameof(DataValue.Parameter))!,
                Expression.Constant(value, typeof(object)))
            : Expression.Constant(value, typeof(object));
    }

    private static object? NormalizeFilterValue(object? value)
    {
        if (value is not JsonElement element)
            return value;

        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            _ => throw new FormatException($"Unsupported JSON filter value [{element.ValueKind}].")
        };
    }
}
