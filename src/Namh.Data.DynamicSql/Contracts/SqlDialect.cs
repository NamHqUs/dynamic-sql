using System.Data;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130
public interface ISqlDialect
{
    string QuoteIdentifier(string identifier);
    string RenderTable(Entity entity);
    string RenderBoolean(bool value);
    string RenderStringLiteral(string value);
    string RenderStringConcatenation(string left, string right);
    string RenderTakePrefix(int count);
    string RenderPaging(int? skip, int? take);
    string RenderPaging(int? skip, int? take, bool hasOrderBy)
        => RenderPaging(skip, take);
}

public sealed class SqlServerDialect : ISqlDialect
{
    public string QuoteIdentifier(string identifier)
        => $"[{identifier.Replace("]", "]]")}]";

    public string RenderTable(Entity entity)
        => string.IsNullOrEmpty(entity.RawSql) || string.Equals(entity.RawSql, entity.Name, StringComparison.Ordinal)
            ? $"dbo.{QuoteIdentifier(entity.Name)}"
            : entity.RawSql;

    public string RenderBoolean(bool value) => value ? "1" : "0";
    public string RenderStringLiteral(string value) => $"N'{value.Replace("'", "''")}'";

    public string RenderStringConcatenation(string left, string right) => $"{left} + {right}";

    public string RenderTakePrefix(int count) => $"TOP {count} ";

    public string RenderPaging(int? skip, int? take)
        => skip is null
            ? string.Empty
            : $" OFFSET {skip} ROWS" +
              (take is null ? string.Empty : $" FETCH NEXT {take} ROWS ONLY");

    public string RenderPaging(int? skip, int? take, bool hasOrderBy)
        => skip is null
            ? RenderPaging(skip, take)
            : (hasOrderBy ? string.Empty : " ORDER BY (SELECT 1)") +
              $" OFFSET {skip} ROWS" +
              (take is null ? string.Empty : $" FETCH NEXT {take} ROWS ONLY");
}

public sealed class SqliteDialect(string? schema = null) : ISqlDialect
{
    public string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"")}\"";

    public string RenderTable(Entity entity)
        => string.IsNullOrEmpty(entity.RawSql) || string.Equals(entity.RawSql, entity.Name, StringComparison.Ordinal)
            ? string.IsNullOrEmpty(schema)
                ? QuoteIdentifier(entity.Name)
                : $"{QuoteIdentifier(schema)}.{QuoteIdentifier(entity.Name)}"
            : entity.RawSql;

    public string RenderBoolean(bool value) => value ? "1" : "0";
    public string RenderStringLiteral(string value) => $"'{value.Replace("'", "''")}'";

    public string RenderStringConcatenation(string left, string right) => $"{left} || {right}";

    public string RenderTakePrefix(int count) => string.Empty;

    public string RenderPaging(int? skip, int? take)
    {
        if (take is null && skip is null)
            return string.Empty;

        return $" LIMIT {(take?.ToString() ?? "-1")}" +
            (skip is null ? string.Empty : $" OFFSET {skip}");
    }
}

public sealed class PostgresDialect(string schema = "public") : ISqlDialect
{
    public string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"")}\"";

    public string RenderTable(Entity entity)
        => string.IsNullOrEmpty(entity.RawSql) || string.Equals(entity.RawSql, entity.Name, StringComparison.Ordinal)
            ? $"{QuoteIdentifier(schema)}.{QuoteIdentifier(entity.Name)}"
            : entity.RawSql;

    public string RenderBoolean(bool value) => value ? "TRUE" : "FALSE";
    public string RenderStringLiteral(string value) => $"'{value.Replace("'", "''")}'";

    public string RenderStringConcatenation(string left, string right) => $"{left} || {right}";

    public string RenderTakePrefix(int count) => string.Empty;

    public string RenderPaging(int? skip, int? take)
    {
        if (take is null && skip is null)
            return string.Empty;

        return $" LIMIT {(take?.ToString() ?? "-1")}" +
            (skip is null ? string.Empty : $" OFFSET {skip}");
    }
}

public static class SqlDialect
{
    public static ISqlDialect ForConnection(IDbConnection connection)
    {
        var connectionType = connection.GetType().FullName;

        if (connectionType?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            return new SqliteDialect();

        if (connectionType?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            return new PostgresDialect();

        return new SqlServerDialect();
    }
}
