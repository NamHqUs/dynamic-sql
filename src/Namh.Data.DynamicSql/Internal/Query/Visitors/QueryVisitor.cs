using System.Linq.Expressions;

namespace Namh.Data.DynamicSql.Internal.Query.Visitors
{
    class QueryVisitor : ExpressionVisitor
    {
        protected readonly QueryContext QueryContext;

        private readonly Lazy<RecordParameterVisitor> _parameterVisitor;
        private readonly Lazy<ClauseVisitor> _clauseVisitor;

        public QueryVisitor(QueryContext queryContext)
        {
            QueryContext = queryContext;

            _parameterVisitor = new Lazy<RecordParameterVisitor>(() => new RecordParameterVisitor(queryContext));
            _clauseVisitor = new Lazy<ClauseVisitor>(() => new ClauseVisitor(queryContext));
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            LambdaExpression lambda = Expression.Lambda(node);
            Delegate fn = lambda.Compile();
            return this.VisitConstant(Expression.Constant(fn.DynamicInvoke(null), node.Type));
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var declaringType = node.Method.DeclaringType;

            if (declaringType == typeof(DbQueryableExtension) || declaringType == typeof(DbQueryableExtension.DbQueryableDummy))
                return _clauseVisitor.Value.Eval(node);

            if (declaringType == typeof(IRecordParameter))
                return _parameterVisitor.Value.Eval(node);

            var instance = Visit(node.Object)!;
            if (instance.Type != node.Method.DeclaringType)
                instance = Expression.Convert(instance, node.Method.DeclaringType!);

            return Expression.Call(instance, node.Method, node.Arguments);
        }
    }
}
