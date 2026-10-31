using System.Data;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public sealed class Db2Dialect(string schema = "MAIN") : SqlDialectBase
{
    public override string Key => "Db2";

    public override IEnumerable<SqlFunctionDefintion> CreateBuiltinFunctions()
        => new Db2FunctionDefinition().CreateAllFunctions();

    protected override IReadOnlyList<string> ForbiddenKeywords =>
    [
        "ALTER", "CALL", "CREATE", "DELETE", "DROP", "GRANT", "INSERT", "INTO",
        "MERGE", "RENAME", "REVOKE", "TRUNCATE", "UPDATE"
    ];

    public override string QuoteIdentifier(string identifier)
        => $"\"{identifier.ToUpperInvariant().Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    public override string RenderTable(Entity entity)
        => entity.IsRawSql
            ? ValidateRawSqlEntity(entity.DbTable)
            : $"{QuoteIdentifier(entity.DbSchema ?? schema)}.{QuoteIdentifier(entity.DbTable ?? entity.Name)}";

    public override string RenderBoolean(bool value) => value ? "1" : "0";
    public override string RenderStringLiteral(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    public override string RenderStringConcatenation(string left, string right) => $"{left} || {right}";
    public override string RenderOrderField(
        string expression,
        string? field,
        string? alias,
        int ordinal)
        => alias is null ? expression : ordinal.ToString();

    public override string RenderGroupField(
        string expression,
        IReadOnlyList<string> fields,
        string? alias)
        => fields.Count > 0 &&
            (expression.StartsWith("ROUND", StringComparison.OrdinalIgnoreCase) ||
             expression.StartsWith("COALESCE", StringComparison.OrdinalIgnoreCase))
            ? fields[0]
            : fields.Count > 0 &&
                expression.Contains("||", StringComparison.Ordinal)
            ? string.Join(", ", fields)
            : expression;
    public override string RenderTakePrefix(int count) => string.Empty;
    public override string RenderCurrentDateTime() => "CURRENT TIMESTAMP";

    public override string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"DAYS(CAST({endDate} AS DATE)) - DAYS(CAST({startDate} AS DATE))";

    public override string RenderPaging(int? skip, int? take)
    {
        if (take is null && skip is null)
            return string.Empty;

        return (skip is null ? string.Empty : $" OFFSET {skip} ROWS") +
            (take is null ? string.Empty : $" FETCH FIRST {take} ROWS ONLY");
    }
}
