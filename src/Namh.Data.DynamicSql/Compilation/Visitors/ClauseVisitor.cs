using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.Compilation.Expressions;
using Namh.Data.DynamicSql.Compilation.Utilities;
using Namh.Data.DynamicSql.Execution;
using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;
using System.Linq.Expressions;
using System.Reflection;

namespace Namh.Data.DynamicSql.Compilation.Visitors;

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

        if (root is QueryDefinitionExpression queryDefinition)
            return new QueryDefinitionVisitor(QueryContext).Eval(queryDefinition.Definition);

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
            case nameof(IDbQueryableExtension.DbQueryableDummy.OrderByField):
            case nameof(IDbQueryableExtension.DbQueryableDummy.OrderByDescendingField):
            case nameof(IDbQueryableExtension.DbQueryableDummy.ThenByField):
            case nameof(IDbQueryableExtension.DbQueryableDummy.ThenByDescendingField):
                return ProcessOrdering(tableContext, methodCall);

            case nameof(IDbQueryableExtension.Any):
                tableContext = new WhereVisitor(tableContext, QueryContext).Eval((LambdaExpression)methodCall.Arguments[1].StripQuotes());
                return new AnyExpression(tableContext);

            case nameof(IDbQueryableExtension.Take):
                return new TakeExpression(tableContext, (int)((ConstantExpression)methodCall.Arguments[1].StripQuotes()).Value!);

            case nameof(IDbQueryableExtension.Skip):
                return new SkipExpression(tableContext, (int)((ConstantExpression)methodCall.Arguments[1].StripQuotes()).Value!);

            case nameof(IDbQueryableExtension.GroupBy):
                foreach (var field in (string[])((ConstantExpression)methodCall.Arguments[1]).Value!)
                {
                    var expression = QueryContext.ParseQueryExpression(
                        tableContext,
                        FieldDefinitionParser.Parse(field));
                    if (expression.ContainsGroupedAggregate())
                        throw new FormatException("Group-by fields cannot contain aggregate expressions.");
                    tableContext.GroupByExpressions.Add(expression);
                }
                return tableContext;

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
        if (methodCall.Arguments[1] is ConstantExpression { Value: IFieldDefinition sortField })
        {
            var projectionAlias = sortField is FieldDefinition { Oper: MatchOperation.Add } fieldDefinition
                ? FindProjectionAlias(tableContext, fieldDefinition.FieldName)
                : null;
            tableContext.Orders.Add(new OrderExpression(
                QueryContext.ParseQueryExpression(tableContext, sortField),
                methodCall.Method.Name.EndsWith("DescendingField", StringComparison.Ordinal),
                projectionAlias));
            return tableContext;
        }

        var fields = (string[])((LambdaExpression)methodCall.Arguments[1].StripQuotes())
            .Compile()
            .DynamicInvoke(new object?[] { null })!;
        var descending = methodCall.Method.Name.EndsWith("Descending", StringComparison.Ordinal);

        foreach (var field in fields)
            tableContext.Orders.Add(new OrderExpression(
                QueryContext.ParseFieldPath(tableContext, field),
                descending,
                FindProjectionAlias(tableContext, field)));

        return tableContext;
    }

    private static string? FindProjectionAlias(TableExpression tableContext, string fieldPath)
        => tableContext.ProjectionItems
            .FirstOrDefault(item => string.Equals(item.Alias, fieldPath, StringComparison.OrdinalIgnoreCase))
            ?.Alias;
}
