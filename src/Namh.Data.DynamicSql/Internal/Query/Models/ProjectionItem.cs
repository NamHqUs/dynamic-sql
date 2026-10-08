using Namh.Data.DynamicSql.Internal.Query.Expressions;
using Namh.Data.DynamicSql.Model;

namespace Namh.Data.DynamicSql.Internal.Query.Models;

class ProjectionItem
{
    public readonly string Alias;
    public readonly SqlExpression Item;
    private readonly ISqlDialect _sqlDialect;

    public ProjectionItem(SqlExpression item, string alias, ISqlDialect sqlDialect)
    {
        Item = item;
        Alias = alias;
        _sqlDialect = sqlDialect;
    }

    public ProjectionItem(FieldExpression fieldExpression)
        : this(fieldExpression, fieldExpression.MetaField.Name, fieldExpression.SqlDialect)
    {
    }

    public ProjectionItem(string tableAlias, Field field, ISqlDialect sqlDialect)
        : this(new FieldExpression(tableAlias, field, sqlDialect))
    {
    }

    public override string ToString()
        => Item is FieldExpression
            ? $"{Item.RenderSql()} {_sqlDialect.QuoteIdentifier(Alias)}"
            : $"({Item.RenderSql()}) {_sqlDialect.QuoteIdentifier(Alias)}";
}
