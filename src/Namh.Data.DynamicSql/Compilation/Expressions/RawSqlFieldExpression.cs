namespace Namh.Data.DynamicSql.Compilation.Expressions;

class RawSqlFieldExpression(string name, string sqlString)
    : FieldExpression(string.Empty, new Field(name) { RawSql = sqlString })
{
}
