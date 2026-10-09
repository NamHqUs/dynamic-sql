namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

class RawSqlFieldExpression(string name, string sqlString)
    : FieldExpression(string.Empty, new Field(name) { RawSql = sqlString })
{
}
