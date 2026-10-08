using System.Linq.Expressions;
using Namh.Data.DynamicSql.Internal.Query.Expressions;
using Namh.Data.DynamicSql.Model;

namespace Namh.Data.DynamicSql.Internal.Query.Visitors;

class RecordParameterVisitor : QueryVisitor
{
    public RecordParameterVisitor(QueryContext queryContext) : base(queryContext)
    {
    }

    public SqlExpression Eval(MethodCallExpression node)
    {
        switch (node.Method.Name)
        {
            case "get_Item":
                return CreateField(node);

            case nameof(IRecordParameter.DbColumn):
                return CreateRawSqlField(node, isDbColumn: true);

            case nameof(IRecordParameter.RawSql):
                return CreateRawSqlField(node, isDbColumn: false);

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
    //e => e.RawSql("Select Count(*) From...")
    private FieldExpression CreateRawSqlField(MethodCallExpression node, bool isDbColumn)
    {
        var argName = (node.Object as ParameterExpression)!.Name;
        var tableContext = QueryContext.Arguments.First(e => e.ArgumentName == argName).TableContext;

        var expression = Visit(node.Arguments[0]);

        var dbColumn = (string) Expression.Lambda(expression).Compile().DynamicInvoke()!;
        var field = new Field(dbColumn) { RawSql = dbColumn };

        return isDbColumn
            ? new FieldExpression(tableContext.Alias, field, QueryContext.SqlDialect)
            : new RawSqlFieldExpression("RawSql", dbColumn, QueryContext.SqlDialect);
    }
}
