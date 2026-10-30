using Namh.Data.DynamicSql.Compilation.Expressions;
using System.Linq.Expressions;

namespace Namh.Data.DynamicSql.Compilation.Visitors;

class RecordParameterVisitor(QueryContext queryContext) : QueryVisitor(queryContext)
{
    public SqlExpression Eval(MethodCallExpression node)
    {
        switch (node.Method.Name)
        {
            case "get_Item":
                return CreateField(node);

            case nameof(IRecordParameter.DbColumn):
                return CreateDbColumnField(node);

            default:
                throw new NotImplementedException(string.Format("not support method {0}.{1}", node.Method.DeclaringType?.Name, node.Method.Name));
        }
    }

    //e => e["Lookup.FieldName"]
    //e => e.Field("Lookup.FieldName")
    private SqlExpression CreateField(MethodCallExpression node)
    {
        var argName = (node.Object as ParameterExpression)!.Name;
        var tableContext = QueryContext.Arguments.First(e => e.ArgumentName == argName).TableContext;

        var expression = Visit(node.Arguments[0]);
        var fieldPath = (string) Expression.Lambda(expression).Compile().DynamicInvoke()!;

        return QueryContext.ParseFieldPath(tableContext, fieldPath);
    }

    //e => e.DbColumn("dbColName")
    private FieldExpression CreateDbColumnField(MethodCallExpression node)
    {
        var argName = (node.Object as ParameterExpression)!.Name;
        var tableContext = QueryContext.Arguments.First(e => e.ArgumentName == argName).TableContext;

        var expression = Visit(node.Arguments[0]);
        var dbColumn = (string) Expression.Lambda(expression).Compile().DynamicInvoke()!;
        return new FieldExpression(tableContext.Alias, new Field(dbColumn, dbColumn));
    }
}
