using Namh.Data.DynamicSql.Internal.Query.Expressions;
using System.Linq.Expressions;

namespace Namh.Data.DynamicSql.Internal.Query.Visitors;

class AggregateVisitor(TableExpression tableContext, QueryContext queryContext)
{
    public AggregateExpression Eval(ConstantExpression expression)
    {
        var fieldName = (string)expression.Value!;
        return (AggregateExpression) queryContext.ParseFieldPath(tableContext, fieldName);
    }
}
