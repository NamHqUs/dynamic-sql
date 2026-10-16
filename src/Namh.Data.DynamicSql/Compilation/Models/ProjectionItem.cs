using Namh.Data.DynamicSql.Compilation.Expressions;

namespace Namh.Data.DynamicSql.Compilation.Models;

class ProjectionItem(SqlExpression item, string alias)
{
    public readonly string Alias = alias;
    public readonly SqlExpression Item = item;

    public ProjectionItem(FieldExpression fieldExpression)
        : this(fieldExpression, fieldExpression.MetaField.Name)
    {
    }

    public ProjectionItem(string tableAlias, Field field)
        : this(new FieldExpression(tableAlias, field))
    {
    }

    public override string ToString() => Alias;
}
