using Namh.Data.DynamicSql.Model;

namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

class FieldExpression(string tableAlias, Field field) : SqlExpression(typeof(DataValue))
{
    public Field MetaField { get; } = field;
    public string TableAlias { get; } = tableAlias; 

    protected override string ToSql() => MetaField.FieldType == FieldType.DbColumn
        ? $"{TableAlias}.[{MetaField.RawSql}]"
        : $"({MetaField.RawSql})";
}
