using Namh.Data.DynamicSql.Internal.Query.Expressions;
using System.Linq.Expressions;

namespace Namh.Data.DynamicSql.Internal.Query.Visitors
{
    class AggregateVisitor 
    {
        private readonly TableExpression _tableContext;
        private readonly QueryContext _queryContext;

        public AggregateVisitor(TableExpression tableContext, QueryContext queryContext)
        {
            _tableContext = tableContext;
            _queryContext = queryContext;
        }

        public AggregateExpression Eval(ConstantExpression expression)
        {
            var fieldName = (string)expression.Value!;
            return (AggregateExpression) _queryContext.ParseFieldPath(_tableContext, fieldName) ;
        }
    }
}
