using Namh.Data.DynamicSql.Internal.Compilation.Expressions;
using Namh.Data.DynamicSql.Internal.Compilation.Utilities;
using Namh.Data.DynamicSql.Internal.Execution;
using System.Linq.Expressions;
using System.Reflection;

namespace Namh.Data.DynamicSql.Internal.Compilation.Visitors;

class ClauseVisitor(QueryContext queryContext)
{
    private static readonly MethodInfo _queryMethod = typeof(IRecordParameter).GetMethod(nameof(IRecordParameter.Query))!;
    protected QueryContext QueryContext { get; } = queryContext;

    public SqlExpression Eval(MethodCallExpression methodCall)
    {
        var statements = new List<MethodCallExpression>() { methodCall };

        while (methodCall.Arguments[0] is MethodCallExpression method)
            statements.Insert(0, methodCall = method);

        var tableContext = CreateTableContext(statements);

        foreach (var statement in statements)
            tableContext = ProcessStatement(tableContext, statement);

        return tableContext;
    }

    private TableExpression CreateTableContext(List<MethodCallExpression> statements)
    {
        var methodCall = statements[0];
        var root = methodCall.Arguments[0];

        if (methodCall.Method == _queryMethod)
        {
            statements.Remove(methodCall);

            var relationPath = (string?)Expression.Lambda(root).Compile().DynamicInvoke();
            var tableContext = QueryContext.Arguments.Peek().TableContext;
            return QueryContext.JoinCollection(tableContext, relationPath);
        }

        var dbQuery = (DbQueryable)((ConstantExpression)root).Value!;
        return QueryContext.CreateTableContext(dbQuery.Provider.Entity);
    }

    private TableExpression ProcessStatement(TableExpression tableContext, MethodCallExpression methodCall)
    {
        switch (methodCall.Method.Name)
        {
            case nameof(IDbQueryableExtension.Select):
                return new SelectVisitor(tableContext, QueryContext).Eval((LambdaExpression)methodCall.Arguments[1].StripQuotes());

            case nameof(IDbQueryableExtension.Where):
                return new WhereVisitor(tableContext, QueryContext).Eval((LambdaExpression)methodCall.Arguments[1].StripQuotes());

            case nameof(IDbQueryableExtension.OrderBy):
            case nameof(IDbQueryableExtension.OrderByDescending):
            case nameof(IDbQueryableExtension.ThenBy):
            case nameof(IDbQueryableExtension.ThenByDescending):
                return ProcessOrdering(tableContext, methodCall);

            case nameof(IDbQueryableExtension.Any):
                tableContext = new WhereVisitor(tableContext, QueryContext).Eval((LambdaExpression)methodCall.Arguments[1].StripQuotes());
                return new AnyExpression(tableContext);

            case nameof(IDbQueryableExtension.Take):
                return new TakeExpression(tableContext, (int)((ConstantExpression)methodCall.Arguments[1].StripQuotes()).Value!);

            case nameof(IDbQueryableExtension.Skip):
                return new SkipExpression(tableContext, (int)((ConstantExpression)methodCall.Arguments[1].StripQuotes()).Value!);

            case nameof(IDbQueryableExtension.Aggregate):
                return (AggregateExpression)QueryContext.ParseFieldPath(tableContext, ((ConstantExpression)methodCall.Arguments[1].StripQuotes()).Value!.ToString()!);

            case nameof(IDbQueryableExtension.Count):
                return new AggregateExpression(tableContext, "Count(*)");

            case nameof(IDbQueryableExtension.ToList):
                return tableContext;

            default:
                throw new NotImplementedException(string.Format("not support method {0}.{1}",
                    methodCall.Method.DeclaringType?.Name??"<UnknownType>",
                    methodCall.Method.Name));
        }
    }

    private TableExpression ProcessOrdering(TableExpression tableContext, MethodCallExpression methodCall)
    {
        var fields = (string[])((LambdaExpression)methodCall.Arguments[1].StripQuotes())
            .Compile()
            .DynamicInvoke(new object?[] { null })!;
        var descending = methodCall.Method.Name.EndsWith("Descending", StringComparison.Ordinal);

        foreach (var field in fields)
            tableContext.Orders.Add(new OrderExpression(
                QueryContext.ParseFieldPath(tableContext, field),
                descending));

        return tableContext;
    }
}
