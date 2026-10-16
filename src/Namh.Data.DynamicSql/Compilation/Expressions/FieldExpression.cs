namespace Namh.Data.DynamicSql.Compilation.Expressions;

class FieldExpression(string tableAlias, Field field) : SqlExpression(typeof(DataValue))
{
    public Field MetaField { get; } = field;
    public string TableAlias { get; } = tableAlias;

    internal override string DebugView
        => $"{GetType().Name}: {TableAlias}.{MetaField.Name}";

}
