using Namh.Data.DynamicSql.Internal.Compilation.Expressions;
using Namh.Data.DynamicSql.Internal.Compilation.Utilities;
using System.Linq.Expressions;

namespace Namh.Data.DynamicSql.Internal.Compilation.Visitors;

class WhereVisitor(TableExpression tableContext, QueryContext queryContext)
    : LambdaVisitor(tableContext, queryContext)
{
    protected override void OnProcessResult(Expression body)
    {
        AddPredicate((SqlExpression)body);
    }

    private void AddPredicate(SqlExpression predicate)
    {
        if (predicate is BinarySqlExpression { Operator: "AND" } conjunction)
        {
            AddPredicate(conjunction.Left);
            AddPredicate(conjunction.Right);
            return;
        }

        (predicate.ContainsGroupedAggregate()
            ? TableContext.SqlHaving
            : TableContext.SqlWhere).Add(predicate);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);

        var leftSql = (SqlExpression)left;
        var rightSql = (SqlExpression)right;
        var oper = rightSql is ValueExpression { Value: null }
            ? node.NodeType == ExpressionType.Equal ? "IS" : "IS NOT"
            : node.NodeType.ToSql();
        return new BinarySqlExpression(leftSql, oper, rightSql, typeof(bool));
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(DataValue) && node.Method.Name == nameof(DataValue.RawCompare))
        {
            var left = Visit(node.Object!);
            var right = ((ConstantExpression)node.Arguments[0]).Value;
            return new CompareSqlExpression((SqlExpression)left, right?.ToString() ?? string.Empty, typeof(bool));
        }

        if (node.Method.DeclaringType == typeof(DataValue) && node.Method.Name == nameof(DataValue.Like))
        {
            var left = (SqlExpression)Visit(node.Object!);
            var value = (SqlExpression)Visit(node.Arguments[0]);
            return new PredicateExpression(left, "LIKE", value);
        }

        if (node.Method.DeclaringType == typeof(DataValue) && node.Method.Name == nameof(DataValue.Parameter))
        {
            var value = ((ConstantExpression)node.Arguments[0]).Value;
            var parameter = QueryContext.AddDbParameter(value, true);
            return new ValueExpression(parameter, typeof(object));
        }

        return base.VisitMethodCall(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        var val = node.Value == null ? null : QueryContext.AddDbParameter(node.Value);
        return new ValueExpression(val, node.Type);
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        object? val = node.Operand is ConstantExpression constant
            ? constant.Value
            : Expression.Lambda(node.Operand).Compile().DynamicInvoke(null);
        var paraName = QueryContext.AddDbParameter(val);
        return new ValueExpression(paraName, node.Type);
    }
}
