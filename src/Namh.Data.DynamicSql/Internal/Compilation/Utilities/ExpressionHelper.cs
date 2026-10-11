using System.Linq.Expressions;

namespace Namh.Data.DynamicSql.Internal.Utilities;

static class ExpressionHelper
{
    public static Expression StripQuotes(this Expression expression)
    {
        while (expression.NodeType == ExpressionType.Quote)
            expression = ((UnaryExpression)expression).Operand;

        return expression;
    }
}
