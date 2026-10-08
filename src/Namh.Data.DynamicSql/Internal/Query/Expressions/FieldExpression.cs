using Namh.Data.DynamicSql.Model;

namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

class FieldExpression(string tableAlias, Field field, ISqlDialect sqlDialect) : SqlExpression(typeof(DataValue))
{
    private readonly ISqlDialect _sqlDialect = sqlDialect;
    internal ISqlDialect SqlDialect => _sqlDialect;
    public Field MetaField { get; } = field;
    public string TableAlias { get; } = tableAlias; 

    internal override string DebugView
        => $"{GetType().Name}: {TableAlias}.{MetaField.Name}";

    internal override string RenderSql() => MetaField.FieldType == FieldType.DbColumn
        ? $"{TableAlias}.{_sqlDialect.QuoteIdentifier(MetaField.RawSql)}"
        : $"({MetaField.RawSql})";
}
