using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.Compilation.Expressions;
using Namh.Data.DynamicSql.Compilation.Models;
using Namh.Data.DynamicSql.Compilation.Utilities;
using System.Linq.Expressions;

namespace Namh.Data.DynamicSql.Compilation.Visitors;

class SelectVisitor(TableExpression tableContext, QueryContext queryContext)
    : LambdaVisitor(tableContext, queryContext)
{
    protected override Expression VisitConstant(ConstantExpression node)
    {
        var value = node.Value;
        if (value is null || value is string || value is bool || value is IConvertible)
            return new ValueExpression(value, node.Type);
        return base.VisitConstant(node);
    }

    ///Dictionary Type .Select("Id", "Name") => .Select(DataRecord)
    protected override void OnProcessResult(Expression body)
    {
        if (body is ConstantExpression @const && @const.Value is DataRecord dataRecord)
        {
            var selectedFields = BuildProjection(TableContext, dataRecord);
            TableContext.ProjectionItems = [.. selectedFields];
        }
    }

    //Dictionary Type .Select(e => new DataRecord{ 
    //        {"Field1, e["Field1"]}
    //        {"Field", e.Query("Collection").Count}, ...
    protected override Expression VisitListInit(ListInitExpression node)
    {
        if (node.Type != typeof(DataRecord))
            return base.VisitListInit(node);

        var dataRecord = new DataRecord();
        foreach (var e in node.Initializers)
        {
            var key = (string)((ConstantExpression)e.Arguments[0]).Value!;

            var expression = e.Arguments[1].StripQuotes();
            expression = expression is UnaryExpression unary ? unary.Operand : expression;
            object val = expression is ConstantExpression constant
                ? constant.Value!
                : Visit(expression);

            dataRecord[key] = (val as ConstantExpression)?.Value
                ?? ((val as UnaryExpression)?.Operand)
                ?? val;
        }

        return Expression.Constant(dataRecord);
    }

    //Calculate, ex:  e["FirstName"] + e["LastName"]
    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);

        var leftVal = (SqlExpression)left;
        var rightVal = (SqlExpression)right;
        var operation = node.NodeType == ExpressionType.Add &&
            (ContainsStringConstant(node.Left) || ContainsStringConstant(node.Right))
                ? "+" : node.NodeType.ToSql();
        return new BinarySqlExpression(leftVal, operation, rightVal, node.Type,
            node.NodeType == ExpressionType.Add &&
            (ContainsStringConstant(node.Left) || ContainsStringConstant(node.Right)));
    }

    private static bool ContainsStringConstant(Expression node)
        => node is ConstantExpression { Value: string } ||
            node is BinaryExpression binary &&
            (ContainsStringConstant(binary.Left) || ContainsStringConstant(binary.Right));

    private IEnumerable<ProjectionItem> BuildProjection(
        TableExpression tableContext, IDictionary<string, object?> memebers, string? parentKey = null)
    {
        foreach (var e in memebers)
            if (e.Value is IDictionary<string, object?> subMembers)
                yield return BuildProjection(tableContext, subMembers, e.Key)
                    .GetEnumerator().Current;
            else
            {
                var sql = e.Value switch
                {
                    SqlExpression expression => expression,
                    IFieldDefinition[] expressions => QueryContext.ParseQueryExpression(tableContext, expressions),
                    IFieldDefinition expression => QueryContext.ParseQueryExpression(tableContext, expression),
                    string fieldPath => QueryContext.ParseFieldPath(tableContext, fieldPath),
                    _ => throw new NotSupportedException($"Unsupported projection value [{e.Value?.GetType().Name ?? "null"}].")
                };

                var alias = parentKey == null ? e.Key : string.Format("{0}.{1}", parentKey, e.Key);
                yield return new ProjectionItem(sql, alias);
            }
    }

}
