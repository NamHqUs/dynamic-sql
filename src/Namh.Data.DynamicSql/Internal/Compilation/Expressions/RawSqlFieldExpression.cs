namespace Namh.Data.DynamicSql.Internal.Compilation.Expressions;

class RawSqlFieldExpression(string name, string sqlString)
    : FieldExpression(string.Empty, new Field(name) { RawSql = sqlString })
{
}
