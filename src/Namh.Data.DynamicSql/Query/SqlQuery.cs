namespace Namh.Data.DynamicSql;

public sealed record SqlQuery(
    string CommandText,
    IReadOnlyList<SqlQueryParameter> Parameters);

public sealed record SqlQueryParameter(
    string Name,
    object? Value);
