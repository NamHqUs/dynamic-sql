using Namh.Data.DynamicSql;

namespace Namh.Data.DynamicSql.Compilation.Expressions;

class TableContextWrapperExpression(TableExpression tableContext, string alias)
    : TableExpression(CreateEntity(tableContext), alias)
{
    private static IEntity CreateEntity(TableExpression tableContext)
        => new RawSqlEntity(
            Name: $"Wrap_{tableContext.Entity.Name}",
            RawSql: $"({tableContext})",
            Fields: tableContext.ProjectionItems.Select(e => (IField)new DbField(e.Alias, e.Alias)).ToList());
}
