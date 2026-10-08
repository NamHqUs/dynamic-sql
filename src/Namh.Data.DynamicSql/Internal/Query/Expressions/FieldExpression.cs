using Namh.Data.DynamicSql.Model;

namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

class FieldExpression(string tableAlias, Field field) : SqlExpression(typeof(DataValue))
{
    public Field MetaField { get; } = field;
    public string TableAlias { get; } = tableAlias;

    internal override string DebugView
        => $"{GetType().Name}: {TableAlias}.{MetaField.Name}";

}
