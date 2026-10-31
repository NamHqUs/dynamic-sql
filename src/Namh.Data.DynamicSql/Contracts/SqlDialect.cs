using System.Data;
using System.Globalization;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130
public interface ISqlDialect
{
    string Key
        => throw new NotSupportedException("A custom SQL dialect must provide a dialect identifier.");
    string QuoteIdentifier(string identifier);
    string RenderTable(Entity entity);
    string RenderBoolean(bool value);
    string RenderStringLiteral(string value);
    string RenderStringConcatenation(string left, string right);
    string RenderTakePrefix(int count);
    string RenderPaging(int? skip, int? take);
    string FormatParameterValue(object? value);
    string RenderCurrentDateTime() => "CURRENT_TIMESTAMP";
    string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"DATEDIFF(day, {startDate}, {endDate})";

    string RenderPaging(int? skip, int? take, bool hasOrderBy)
        => RenderPaging(skip, take);

    string RenderQueryWithParameters(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
        => parameters.Count == 0
            ? commandText
            : commandText + $"\n-- Parameters: {string.Join(", ", parameters.Select(parameter =>
                $"{parameter.Name} = {FormatParameterValue(parameter.Value)}"))}";
}

public sealed class SqlServerDialect : ISqlDialect
{
    public string Key => "SqlServer";

    public string QuoteIdentifier(string identifier)
        => $"[{identifier.Replace("]", "]]")}]";

    public string RenderTable(Entity entity)
        => entity.IsRawSql
            ? RawSqlEntitySqlGuard.ValidateAndReturn(entity.DbTable, RawSqlEntityDialect.SqlServer)
            : $"{QuoteIdentifier(entity.DbSchema ?? "dbo")}.{QuoteIdentifier(entity.DbTable ?? entity.Name)}";

    public string RenderBoolean(bool value) => value ? "1" : "0";
    public string RenderStringLiteral(string value) => $"N'{value.Replace("'", "''")}'";

    public string RenderStringConcatenation(string left, string right) => $"{left} + {right}";

    public string RenderTakePrefix(int count) => $"TOP {count} ";
    public string FormatParameterValue(object? value) => SqlDialect.FormatParameterValue(value);
    public string RenderCurrentDateTime() => "GETDATE()";

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

    public string RenderQueryWithParameters(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
    {
        if (parameters.Count == 0)
            return commandText;

        var declarations = parameters
            .Select(parameter =>
                $"{parameter.Name} {GetSqlServerType(parameter.Value)} = {FormatParameterValue(parameter.Value)}")
            .ToArray();
        var declarationText = declarations.Length == 1
            ? "DECLARE " + declarations[0] + ";"
            : "DECLARE " + declarations[0] + ",\n\t" +
                string.Join(",\n\t", declarations.Skip(1)) + ";";

        return declarationText + "\n" + commandText;
    }

    private static string GetSqlServerType(object? value)
        => value switch
        {
            null => "nvarchar(4000)",
            string => "nvarchar(4000)",
            bool => "bit",
            byte or short or int or long => "int",
            float or double => "float",
            decimal => "decimal(38, 18)",
            DateTime or DateTimeOffset => "datetime2",
            Guid => "uniqueidentifier",
            _ => "nvarchar(4000)"
        };
}

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

public sealed class PostgresDialect(string schema = "public") : ISqlDialect
{
    public string Key => "Postgres";

    public string QuoteIdentifier(string identifier)
        => $"\"{identifier.ToLowerInvariant().Replace("\"", "\"\"")}\"";

    public string RenderTable(Entity entity)
        => entity.IsRawSql
            ? RawSqlEntitySqlGuard.ValidateAndReturn(entity.DbTable, RawSqlEntityDialect.Postgres)
            : $"{QuoteIdentifier(entity.DbSchema ?? schema)}.{QuoteIdentifier(entity.DbTable ?? entity.Name)}";

    public string RenderBoolean(bool value) => value ? "1" : "0";
    public string RenderStringLiteral(string value) => $"'{value.Replace("'", "''")}'";

    public string RenderStringConcatenation(string left, string right) => $"{left} || {right}";

    public string RenderTakePrefix(int count) => string.Empty;
    public string FormatParameterValue(object? value) => SqlDialect.FormatParameterValue(value);
    public string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"CAST({endDate} AS DATE) - CAST({startDate} AS DATE)";

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
    internal static string FormatParameterValue(object? value)
        => value switch
        {
            null => "NULL",
            string text => $"'{text.Replace("'", "''", StringComparison.Ordinal)}'",
            bool boolean => boolean ? "1" : "0",
            DateTime dateTime => $"'{dateTime.ToString("O", CultureInfo.InvariantCulture)}'",
            DateTimeOffset dateTimeOffset => $"'{dateTimeOffset.ToString("O", CultureInfo.InvariantCulture)}'",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "NULL",
            _ => $"'{value}'"
        };

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
