using Namh.Data.DynamicSql.Internal.Compilation.Expressions;
using Namh.Data.DynamicSql.Internal.Compilation.Models;
using Namh.Data.DynamicSql.Internal.Compilation.Visitors;
using System.Linq.Expressions;

namespace Namh.Data.DynamicSql.Internal.Compilation;

internal sealed record CompiledQuery(
    string CommandText,
    IReadOnlyList<DbParameter> Parameters,
    IReadOnlyList<ProjectionItem> ProjectionItems);

internal sealed class SqlQueryCompiler(
    IMetaProvider metaProvider,
    ISqlDialect sqlDialect,
    bool includeArchive)
{
    public CompiledQuery Compile(Expression expression)
    {
        var queryContext = new QueryContext(metaProvider, includeArchive);
        var tableExpression = (TableExpression)new QueryVisitor(queryContext).Visit(expression);
        var generatedSql = new SqlStatementGenerator(sqlDialect).Generate(
            tableExpression,
            queryContext.DbParameters);

        return new CompiledQuery(
            generatedSql.CommandText,
            generatedSql.Parameters,
            tableExpression.ProjectionItems.ToArray());
    }
}
