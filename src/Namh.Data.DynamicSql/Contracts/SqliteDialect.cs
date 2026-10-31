using System.Data;
using System.Globalization;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public sealed class SqliteDialect(string? schema = null) : ISqlDialect
{
    public string Key => "Sqlite";

    public string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"")}\"";

    public string RenderTable(Entity entity)
        => entity.IsRawSql
            ? RawSqlEntitySqlGuard.ValidateAndReturn(entity.DbTable, RawSqlEntityDialect.Sqlite)
            : string.IsNullOrEmpty(entity.DbSchema ?? schema)
                ? QuoteIdentifier(entity.DbTable ?? entity.Name)
                : $"{QuoteIdentifier(entity.DbSchema ?? schema!)}.{QuoteIdentifier(entity.DbTable ?? entity.Name)}";

    public string RenderBoolean(bool value) => value ? "1" : "0";
    public string RenderStringLiteral(string value) => $"'{value.Replace("'", "''")}'";

    public string RenderStringConcatenation(string left, string right) => $"{left} || {right}";

    public string RenderTakePrefix(int count) => string.Empty;
    public string FormatParameterValue(object? value) => SqlDialect.FormatParameterValue(value);
    public string RenderCurrentDateTime() => "datetime('now', 'localtime')";
    public string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"CAST(julianday(date({endDate})) - julianday(date({startDate})) AS INTEGER)";

    public string RenderPaging(int? skip, int? take)
    {
        if (take is null && skip is null)
            return string.Empty;

        return $" LIMIT {(take?.ToString() ?? "-1")}" +
            (skip is null ? string.Empty : $" OFFSET {skip}");
    }
}

