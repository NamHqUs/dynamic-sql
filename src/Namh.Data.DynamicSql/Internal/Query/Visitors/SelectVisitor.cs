using Namh.Data.DynamicSql.Internal.Query.Expressions;
using Namh.Data.DynamicSql.Internal.Query.Models;
using Namh.Data.DynamicSql.Internal.Utilities;
using System.Linq.Expressions;

namespace Namh.Data.DynamicSql.Internal.Query.Visitors
{
    class SelectVisitor : LambdaVisitor
    {
        public SelectVisitor(TableExpression tableContext, QueryContext queryContext)
            : base(tableContext, queryContext) { }

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
                var key = (string)((ConstantExpression)Visit(e.Arguments[0])).Value!;

                var expression = e.Arguments[1].StripQuotes();
                expression = expression is UnaryExpression unary ? unary.Operand : expression;
                object val = Visit(expression);

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

            var leftVal = left is TableExpression ? $"({left})" : Format(left);
            var rightVal = right is TableExpression ? $"({right})" : Format(right);

            return new ValueExpression($"{leftVal} {node.NodeType.ToSql()} {rightVal}", node.Type);
        }

        private IEnumerable<ProjectionItem> BuildProjection(
            TableExpression tableContext, IDictionary<string, object?> memebers, string? parentKey = null)
        {
            foreach (var e in memebers)
                if (e.Value is IDictionary<string, object?> subMembers)
                    yield return BuildProjection(tableContext, subMembers, e.Key)
                        .GetEnumerator().Current;
                else
                {
                    var sql = e.Value as SqlExpression
                        ?? QueryContext.ParseFieldPath(tableContext, e.Value!.ToString()!);

                    var alias = parentKey == null ? e.Key : string.Format("{0}.{1}", parentKey, e.Key);
                    yield return new ProjectionItem(sql, alias);
                }
        }

        private static string Format(Expression node)
        { 
            if (node is ConstantExpression @const && @const.Value is string text)
                return $"'{text}'";

            return node.ToString();
        }
    }
}
