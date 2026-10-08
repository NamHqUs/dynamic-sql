namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

internal sealed class SubqueryExpression(TableExpression query) : SqlExpression(query.Type)
{
    public TableExpression Query { get; } = query;
}
