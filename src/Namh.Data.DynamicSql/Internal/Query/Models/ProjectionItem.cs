using Namh.Data.DynamicSql.Internal.Query.Expressions;
using Namh.Data.DynamicSql.Model;

namespace Namh.Data.DynamicSql.Internal.Query.Models;

class ProjectionItem
{
    public readonly string Alias;
    public readonly SqlExpression Item;

    public ProjectionItem(SqlExpression item, string alias)
    {
        Item = item;
        Alias = alias;
    }

    public ProjectionItem(FieldExpression fieldExpression)
    {
        Item = fieldExpression;
        Alias = fieldExpression.MetaField.Name;
    }

    public ProjectionItem(string tableAlias, Field field) : this(new FieldExpression(tableAlias, field))
    {
    }

    public override string ToString()
        => Item is FieldExpression
            ? $"{Item} [{Alias}]"
            : $"({Item}) [{Alias}]";
}
