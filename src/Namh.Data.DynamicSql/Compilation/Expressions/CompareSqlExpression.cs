namespace Namh.Data.DynamicSql.Compilation.Expressions;

internal sealed class CompareSqlExpression(SqlExpression left, string comparison, Type type) : SqlExpression(type)
{
    public SqlExpression Left { get; } = left;
    public string Comparison { get; } = comparison;
}
