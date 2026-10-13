using Namh.Data.DynamicSql.Internal.Compilation.Expressions;
using Namh.Data.DynamicSql.Internal.Compilation.Models;
using Namh.Data.DynamicSql.Internal.Compilation.Visitors;
using System.Linq.Expressions;

namespace Namh.Data.DynamicSql.Internal.Compilation;

internal sealed record CompiledQuery(
    string CommandText,
    IReadOnlyList<DbParameter> Parameters,
    IReadOnlyList<ProjectionItem> ProjectionItems,
    string? TotalRecordsCommandText);

internal sealed class SqlQueryCompiler(
    IMetaProvider metaProvider,
    ISqlDialect sqlDialect,
    bool includeArchive)
{
    internal const int TotalRecordsLimit = 1000;
    public CompiledQuery Compile(Expression expression, bool includeTotalRecords = false)
    {
        var queryContext = new QueryContext(metaProvider, includeArchive);
        var tableExpression = (TableExpression)new QueryVisitor(queryContext).Visit(expression);
        var statementGenerator = new SqlStatementGenerator(sqlDialect);
        var generatedSql = statementGenerator.Generate(tableExpression, queryContext.DbParameters);
        var totalRecordsSql = includeTotalRecords
            ? statementGenerator.GenerateTotalRecords(
                tableExpression,
                queryContext.DbParameters,
                TotalRecordsLimit + 1)
            : null;

        return new CompiledQuery(
            generatedSql.CommandText,
            generatedSql.Parameters,
            tableExpression.ProjectionItems.ToArray(),
            totalRecordsSql?.CommandText);
    }
}
