using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
using System.Linq.Expressions;
using System.Text.Json;

namespace Namh.Data.DynamicSql.Execution;

internal static class DbQueryFromQueryDto
{
    public static IDbQueryable BuildQueryable(DynamicSqlContext context, string yml)
        => BuildQueryable(context, QueryDefinitionParser.ParseFromYml(yml));
    public static IDbQueryable BuildQueryable(DynamicSqlContext context, QueryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.Projection);

        var query = context.Query(
            definition.Entity,
            definition.Relations);

        if (definition.Filter is not null)
            query = query.Where(CreateFilter(definition.Filter));

        if (definition.Projection.Count > 0)
        {
            var selection = new DataRecord();
            foreach (var field in definition.Projection)
            {
                var alias = string.IsNullOrWhiteSpace(field.Alias)
                    ? field.ToText()
                    : field.Alias;
                selection[alias] = field.Fields;
            }

            query = query.Select(selection);
        }

        foreach (var sortGroup in definition.Sorts.Select((sort, index) => (sort, index)))
        {
            query = sortGroup.index == 0
                ? sortGroup.sort.Direction == QuerySortDirection.Ascending
                    ? query.OrderByField(sortGroup.sort.Field)
                    : query.OrderByDescendingField(sortGroup.sort.Field)
                : sortGroup.sort.Direction == QuerySortDirection.Ascending
                    ? query.ThenByField(sortGroup.sort.Field)
                    : query.ThenByDescendingField(sortGroup.sort.Field);
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

    private static Expression<Func<IRecordParameter, bool>> CreateFilter(FilterDefinition filter)
    {
        var parameter = Expression.Parameter(typeof(IRecordParameter), "record");
        var body = CreateFilterBody(filter, parameter);

        return Expression.Lambda<Func<IRecordParameter, bool>>(body, parameter);
    }

    private static Expression CreateFilterBody(
        IFilterDefintion filter,
        ParameterExpression parameter)
        => filter switch
        {
            FilterDefinition group => CreateLogicalFilter(group, parameter),
            FilterItemDefinition binary => CreateCondition(binary, parameter),
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };

    private static Expression CreateLogicalFilter(
        FilterDefinition group,
        ParameterExpression parameter)
    {
        if (group.Filters.Count == 0)
            return Expression.Constant(true);

        var expression = CreateFilterBody(group.Filters[0], parameter);
        foreach (var filter in group.Filters.Skip(1))
        {
            var next = CreateFilterBody(filter, parameter);
            var logicalOperation = filter is FilterItemDefinition { LogicalOperator: not null } binary
                ? binary.LogicalOperator.Value
                : group.LogicalOperation;
            expression = logicalOperation == LogicalOperator.And
                ? Expression.AndAlso(expression, next)
                : Expression.OrElse(expression, next);
        }

        return expression;
    }

    private static Expression CreateCondition(
        FilterItemDefinition filter,
        ParameterExpression parameter)
    {
        var field = CreateFieldAccess(filter.Left, parameter);
        var value = filter.Operation is BinaryOperator.Between or BinaryOperator.NotBetween or BinaryOperator.In or BinaryOperator.NotIn
            ? null
            : CreateFilterValue(filter, parameter);

        if (filter.OperatorId is not null)
        {
            if (string.IsNullOrWhiteSpace(filter.CustomSqlTemplate))
                throw new FormatException($"Custom operator '{filter.OperatorId}' has no trusted SQL template.");
            return Expression.Call(
                field,
                typeof(DataValue).GetMethod(nameof(DataValue.CompareTemplate))!,
                value!,
                Expression.Constant(filter.CustomSqlTemplate));
        }

        if (filter.Operation == BinaryOperator.Like)
        {
            if (field.Type != typeof(DataValue))
                throw new FormatException("The left side of a LIKE filter must be a field reference.");

            return Expression.Call(
                field,
                typeof(DataValue).GetMethod(nameof(DataValue.Like))!,
                value!);
        }

        return filter.Operation switch
        {
            BinaryOperator.Equals => Expression.Equal(field, value!),
            BinaryOperator.NotEquals => Expression.NotEqual(field, value!),
            BinaryOperator.GreaterThan => Expression.GreaterThan(field, value!),
            BinaryOperator.LessThan => Expression.LessThan(field, value!),
            BinaryOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(field, value!),
            BinaryOperator.LessThanOrEqual => Expression.LessThanOrEqual(field, value!),
            BinaryOperator.NotLike => Expression.Call(
                field,
                typeof(DataValue).GetMethod(nameof(DataValue.NotLike))!,
                value!),
            BinaryOperator.IsNull => Expression.Call(
                field,
                typeof(DataValue).GetMethod(nameof(DataValue.IsNull))!),
            BinaryOperator.IsNotNull => Expression.Call(
                field,
                typeof(DataValue).GetMethod(nameof(DataValue.IsNotNull))!),
            BinaryOperator.Between or BinaryOperator.NotBetween => CreateRangeCondition(filter, field, parameter),
            BinaryOperator.In or BinaryOperator.NotIn => CreateSetCondition(filter, field, parameter),
            _ => throw new ArgumentOutOfRangeException(nameof(filter.Operation))
        };
    }

    private static Expression CreateRangeCondition(
        FilterItemDefinition filter,
        Expression field,
        ParameterExpression parameter)
    {
        var values = GetMultipleFilterValues(filter, parameter, 2);
        return Expression.Call(
            field,
            typeof(DataValue).GetMethod(
                filter.Operation == BinaryOperator.Between
                    ? nameof(DataValue.Between)
                    : nameof(DataValue.NotBetween))!,
            values[0],
            values[1]);
    }

    private static Expression CreateSetCondition(
        FilterItemDefinition filter,
        Expression field,
        ParameterExpression parameter)
    {
        var values = GetMultipleFilterValues(filter, parameter, null);
        var array = Expression.NewArrayInit(typeof(object), values);
        return Expression.Call(
            field,
            typeof(DataValue).GetMethod(
                filter.Operation == BinaryOperator.In
                    ? nameof(DataValue.In)
                    : nameof(DataValue.NotIn))!,
            array);
    }

    private static Expression[] GetMultipleFilterValues(
        FilterItemDefinition filter,
        ParameterExpression parameter,
        int? expectedCount)
    {
        var fields = filter.Right switch
        {
            GroupFieldDefintion group => group.Fields,
            ConstFieldDefinition { Value: string value } when
                filter.Operation is BinaryOperator.Between or BinaryOperator.NotBetween or BinaryOperator.In or BinaryOperator.NotIn
                => value
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => (IFieldDefinition)new ConstFieldDefinition(item))
                    .ToArray(),
            null => Array.Empty<IFieldDefinition>(),
            _ => new[] { filter.Right }
        };
        if (fields.Length == 0 || expectedCount is not null && fields.Length != expectedCount)
            throw new FormatException(
                expectedCount is null
                    ? "Set comparison values cannot be empty."
                    : $"The {filter.Operation} operator requires exactly {expectedCount} values.");

        return fields
            .Select(field => CreateFilterValue(
                new FilterItemDefinition(null, filter.Left, filter.Operation, field)
                {
                    Parameterized = filter.Parameterized
                },
                parameter))
            .Select(value => value.Type == typeof(object)
                ? value
                : Expression.Convert(value, typeof(object)))
            .ToArray();
    }

    private static string GetFilterFieldName(IFieldDefinition field)
        => field is FieldDefinition fieldDto
            ? fieldDto.FieldName
            : throw new FormatException(
                $"Filter fields must be field references. Received [{field.GetType().Name}].");

    private static Expression CreateFieldAccess(IFieldDefinition field, ParameterExpression parameter)
        => field switch
        {
            FieldDefinition fieldDto => Expression.Call(
                parameter,
                typeof(IRecordParameter).GetProperty("Item")!.GetMethod!,
                Expression.Constant(GetFilterFieldName(fieldDto))),
            FunctionFieldDefinition or GroupFieldDefintion
                => Expression.Call(
                    typeof(DataValue).GetMethod(nameof(DataValue.QueryExpression))!,
                    Expression.Constant(field, typeof(IFieldDefinition))),
            _ => throw new FormatException(
                $"Filter field expressions must be field references or query expressions. Received [{field.GetType().Name}].")
        };

    private static Expression CreateFilterValue(
        FilterItemDefinition filter,
        ParameterExpression parameter)
    {
        return filter.Right switch
        {
            null => Expression.Constant(null, typeof(object)),
            FieldDefinition field => CreateFieldAccess(field, parameter),
            FunctionFieldDefinition or GroupFieldDefintion
                => CreateFieldAccess(filter.Right, parameter),
            ConstFieldDefinition constant => CreateFilterConstant(constant.Value, filter.Parameterized),
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
