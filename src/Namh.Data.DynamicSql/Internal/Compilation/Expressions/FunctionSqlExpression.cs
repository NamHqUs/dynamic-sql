namespace Namh.Data.DynamicSql.Internal.Compilation.Expressions;

internal sealed class FunctionSqlExpression(
    FunctionDefinition definition,
    IReadOnlyList<FunctionSqlArgument?> arguments) : SqlExpression(definition.ResultType)
{
    public FunctionDefinition Definition { get; } = definition;
    public IReadOnlyList<FunctionSqlArgument?> Arguments { get; } = arguments;
}

internal abstract record FunctionSqlArgument;

internal sealed record FunctionSqlExpressionArgument(SqlExpression Expression) : FunctionSqlArgument;

internal sealed record FunctionSqlTokenArgument(string Token) : FunctionSqlArgument;

internal sealed record FunctionSqlTypeArgument(SqlTypeDefinition SqlType) : FunctionSqlArgument;
