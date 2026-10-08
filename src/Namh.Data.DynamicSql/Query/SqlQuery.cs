#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public sealed record SqlQuery(
    string CommandText,
    IReadOnlyList<SqlQueryParameter> Parameters);

public sealed record SqlQueryParameter(
    string Name,
    object? Value);
