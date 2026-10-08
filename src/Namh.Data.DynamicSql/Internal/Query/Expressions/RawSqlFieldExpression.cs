using Namh.Data.DynamicSql.Model;

namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

class RawSqlFieldExpression(string name, string sqlString, ISqlDialect sqlDialect)
    : FieldExpression(string.Empty, new Field(name) { RawSql = sqlString }, sqlDialect)
{
    internal override string RenderSql() => $"({MetaField.RawSql})";
}
