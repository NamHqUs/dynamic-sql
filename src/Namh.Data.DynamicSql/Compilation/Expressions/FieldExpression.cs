namespace Namh.Data.DynamicSql.Compilation.Expressions;

class FieldExpression(string tableAlias, IField field) : SqlExpression(typeof(DataValue))
{
    public IField MetaField { get; } = field;
    public string TableAlias { get; } = tableAlias;

    internal override string DebugView
        => $"{GetType().Name}: {TableAlias}.{MetaField.Name}";

}
