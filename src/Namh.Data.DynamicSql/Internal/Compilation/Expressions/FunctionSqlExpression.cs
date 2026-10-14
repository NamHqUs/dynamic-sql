namespace Namh.Data.DynamicSql.Internal.Compilation.Expressions;

internal sealed class FunctionSqlExpression(
    string name,
    IReadOnlyList<SqlExpression> arguments,
    Type type) : SqlExpression(type)
{
    public string Name { get; } = name;
    public IReadOnlyList<SqlExpression> Arguments { get; } = arguments;
}
