using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.QueryDtoParsing;
using System.Linq.Expressions;
using System.Text.Json;

namespace Namh.Data.DynamicSql.Internal.Execution;

internal static class DbQueryFromQueryDto
{
    public static IDbQueryable BuildQueryable(DynamicSqlContext context, string json)
        => BuildQueryable(context, QueryDtoParser.Parse(json));
    public static IDbQueryable BuildQueryable(DynamicSqlContext context, QueryDto definition)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.GroupBy);
        ArgumentNullException.ThrowIfNull(definition.Fields);

        var query = context.Query(definition.Entity, definition.IncludeArchive);

        if (definition.GroupBy.Count > 0)
            query = query.GroupBy(definition.GroupBy.ToArray());

        if (definition.Filter is not null)
            query = query.Where(CreateFilter(definition.Filter));

        if (definition.Fields.Count > 0)
        {
            var selection = new DataRecord();
            foreach (var field in definition.Fields)
            {
                var alias = string.IsNullOrWhiteSpace(field.Alias)
                    ? field.Field is FieldDto simpleField
                        ? simpleField.FieldName
                        : throw new FormatException("Calculated projection fields require an alias.")
                    : field.Alias;
                selection[alias] = field.Field;
            }

            query = query.Select(selection);
        }

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

    private static Expression<Func<IRecordParameter, bool>> CreateFilter(FilterDto filter)
    {
        var parameter = Expression.Parameter(typeof(IRecordParameter), "record");
        var body = CreateFilterBody(filter, parameter);

        return Expression.Lambda<Func<IRecordParameter, bool>>(body, parameter);
    }

    private static Expression CreateFilterBody(
        IFilterDto filter,
        ParameterExpression parameter)
        => filter switch
        {
            FilterDto group => CreateLogicalFilter(group, parameter),
            FilterItemDto binary => CreateCondition(binary, parameter),
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };

    private static Expression CreateLogicalFilter(
        FilterDto group,
        ParameterExpression parameter)
    {
        if (group.Filters.Count == 0)
            return Expression.Constant(true);

        var expression = CreateFilterBody(group.Filters[0], parameter);
        foreach (var filter in group.Filters.Skip(1))
        {
            var next = CreateFilterBody(filter, parameter);
            var logicalOperation = filter is FilterItemDto { LogicalOperator: not null } binary
                ? binary.LogicalOperator.Value
                : group.LogicalOperation;
            expression = logicalOperation == LogicalOperator.And
                ? Expression.AndAlso(expression, next)
                : Expression.OrElse(expression, next);
        }

        return expression;
    }

    private static Expression CreateCondition(
        FilterItemDto filter,
        ParameterExpression parameter)
    {
        var field = CreateFieldAccess(filter.Left, parameter);
        var value = CreateFilterValue(filter, parameter);

        if (filter.Operation == BinaryOperator.Like)
        {
            if (field.Type != typeof(DataValue))
                throw new FormatException("The left side of a LIKE filter must be a field reference.");

            return Expression.Call(
                field,
                typeof(DataValue).GetMethod(nameof(DataValue.Like))!,
                value);
        }

        return filter.Operation switch
        {
            BinaryOperator.Equals => Expression.Equal(field, value),
            BinaryOperator.NotEquals => Expression.NotEqual(field, value),
            BinaryOperator.GreaterThan => Expression.GreaterThan(field, value),
            BinaryOperator.LessThan => Expression.LessThan(field, value),
            BinaryOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(field, value),
            BinaryOperator.LessThanOrEqual => Expression.LessThanOrEqual(field, value),
            _ => throw new ArgumentOutOfRangeException(nameof(filter.Operation))
        };
    }

    private static string GetFilterFieldName(IFieldDto field)
        => field is FieldDto fieldDto
            ? fieldDto.FieldName
            : throw new FormatException(
                $"Filter fields must be field references. Received [{field.GetType().Name}].");

    private static Expression CreateFieldAccess(IFieldDto field, ParameterExpression parameter)
        => field is FieldDto fieldDto
            ? Expression.Call(
                parameter,
                typeof(IRecordParameter).GetProperty("Item")!.GetMethod!,
                Expression.Constant(GetFilterFieldName(fieldDto)))
            : throw new FormatException(
                $"Filter field expressions must be field references. Received [{field.GetType().Name}].");

    private static Expression CreateFilterValue(
        FilterItemDto filter,
        ParameterExpression parameter)
    {
        return filter.Right switch
        {
            null => Expression.Constant(null, typeof(object)),
            FieldDto field => CreateFieldAccess(field, parameter),
            ConstFieldDto constant => CreateFilterConstant(constant.Value, filter.Parameterized),
            _ => throw new FormatException(
                $"Filter right expressions must be field references or constants. Received [{filter.Right.GetType().Name}].")
        };
    }

    private static Expression CreateFilterConstant(object? value, bool parameterized)
    {
        var normalizedValue = NormalizeFilterValue(value);
        return parameterized
            ? Expression.Call(
                typeof(DataValue).GetMethod(nameof(DataValue.Parameter))!,
                Expression.Constant(normalizedValue, typeof(object)))
            : Expression.Constant(normalizedValue, typeof(object));
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
