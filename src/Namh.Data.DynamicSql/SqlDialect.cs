using Namh.Data.DynamicSql.Model;
using System.Data;

namespace Namh.Data.DynamicSql;

public interface ISqlDialect
{
    string QuoteIdentifier(string identifier);
    string RenderTable(Entity entity);
    string RenderBoolean(bool value);
    string RenderStringConcatenation(string left, string right);
    string RenderTakePrefix(int count);
    string RenderPaging(int? skip, int? take);
}

public sealed class SqlServerDialect : ISqlDialect
{
    public string QuoteIdentifier(string identifier)
        => $"[{identifier.Replace("]", "]]")}]";

    public string RenderTable(Entity entity)
        => string.IsNullOrEmpty(entity.RawSql)
            ? $"dbo.{QuoteIdentifier(entity.Name)}"
            : entity.RawSql;

    public string RenderBoolean(bool value) => value ? "1" : "0";

    public string RenderStringConcatenation(string left, string right) => $"{left} + {right}";

    public string RenderTakePrefix(int count) => $"TOP {count} ";

    public string RenderPaging(int? skip, int? take)
        => skip is null
            ? string.Empty
            : $" OFFSET {skip} ROWS" +
              (take is null ? string.Empty : $" FETCH NEXT {take} ROWS ONLY");
}

public sealed class SqliteDialect(string? schema = null) : ISqlDialect
{
    public string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"")}\"";

    public string RenderTable(Entity entity)
        => string.IsNullOrEmpty(entity.RawSql)
            ? string.IsNullOrEmpty(schema)
                ? QuoteIdentifier(entity.Name)
                : $"{QuoteIdentifier(schema)}.{QuoteIdentifier(entity.Name)}"
            : entity.RawSql;

    public string RenderBoolean(bool value) => value ? "1" : "0";

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
        => string.IsNullOrEmpty(entity.RawSql)
            ? $"{QuoteIdentifier(schema)}.{QuoteIdentifier(entity.Name)}"
            : entity.RawSql;

    public string RenderBoolean(bool value) => value ? "TRUE" : "FALSE";

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
