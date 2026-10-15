namespace Namh.Data.DynamicSql.Internal.Compilation.Expressions;

internal static class SqlExpressionExtensions
{
    public static bool ContainsGroupedAggregate(this SqlExpression expression)
        => expression switch
        {
            AggregateExpression { GroupWithQuery: true } => true,
            FunctionSqlExpression function =>
                function.Name is "COUNT" or "SUM" or "AVG" ||
                function.Arguments.Any(ContainsGroupedAggregate),
            BinarySqlExpression binary =>
                binary.Left.ContainsGroupedAggregate() || binary.Right.ContainsGroupedAggregate(),
            CompareSqlExpression compare => compare.Left.ContainsGroupedAggregate(),
            PredicateExpression predicate =>
                predicate.Left.ContainsGroupedAggregate() ||
                (predicate.Right?.ContainsGroupedAggregate() ?? false),
            ProjectionExpression projection => projection.Expression.ContainsGroupedAggregate(),
            _ => false
        };
}
