using Namh.Data.DynamicSql.Model;

namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

class TableContextWrapperExpression(TableExpression tableContext, string alias)
    : TableExpression(CreateEntity(tableContext), alias, tableContext.SqlDialect)
{
    private static Entity CreateEntity(TableExpression tableContext)
        => new(
            Name: $"Wrap_{tableContext.Entity.Name}",
            RawSql: $"({tableContext})",
            Fields: tableContext.ProjectionItems.Select(e => new Field(e.Alias)).ToList());
}
