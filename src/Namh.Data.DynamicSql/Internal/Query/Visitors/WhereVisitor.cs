using Namh.Data.DynamicSql.Internal.Query.Expressions;
using Namh.Data.DynamicSql.Internal.Utilities;
using System.Linq.Expressions;
using System.Text;

namespace Namh.Data.DynamicSql.Internal.Query.Visitors;

class WhereVisitor(TableExpression tableContext, QueryContext queryContext) : LambdaVisitor(tableContext, queryContext)
{
    protected override void OnProcessResult(Expression body)
    {

        var sqlFilter = ((SqlExpression)body).ToString();

        if (TableContext.SqlWhere == null || TableContext.SqlWhere.Length == 0)
            TableContext.SqlWhere = new StringBuilder(sqlFilter);
        else
            TableContext.SqlWhere.Append($" AND {sqlFilter}");
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);

        var sqlLeft = (left is AggregateExpression) ? $"({left})" : left.ToString();
        var sqlRight = (right is AggregateExpression) ? $"({right})" : right.ToString();

        var oper = sqlRight != "NULL" ? node.NodeType.ToSql() :
            node.NodeType == ExpressionType.Equal ? "IS" : "IS NOT";

        return new ValueExpression($"({sqlLeft} {oper} {sqlRight})", typeof(bool));
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(DataValue) && node.Method.Name == nameof(DataValue.RawCompare))
        {
            var left = Visit(node.Object);
            var right = ((ConstantExpression)node.Arguments[0]).Value;
            return new ValueExpression($"({left} {right})", typeof(bool));
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
