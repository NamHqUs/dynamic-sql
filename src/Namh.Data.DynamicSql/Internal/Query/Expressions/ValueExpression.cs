namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

class ValueExpression(object? value, Type type) : SqlExpression(type)
{
    public readonly object? Value =value;

    protected override string ToSql() => Value?.ToString() ?? "NULL";
}
