#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public sealed class SapHanaDialect(string schema = "NORTHSTAR") : SqlDialectBase
{
    public override string Key => "SapHana";

    public override IEnumerable<SqlFunctionDefintion> CreateBuiltinFunctions()
        => new SapHanaFunctionDefinitionBuiltin().CreateAllFunctions();

    protected override IReadOnlyList<string> ForbiddenKeywords =>
    [
        "ALTER", "CREATE", "DELETE", "DROP", "INSERT", "INTO", "TRUNCATE", "UPDATE"
    ];

    public override string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    public override string RenderTable(Entity entity)
        => entity.IsRawSql
            ? ValidateRawSqlEntity(entity.DbTable)
            : $"{QuoteIdentifier(schema)}.{QuoteIdentifier(entity.DbTable ?? entity.Name)}";

    public override string RenderBoolean(bool value) => value ? "TRUE" : "FALSE";
    public override string RenderStringLiteral(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    public override string RenderStringConcatenation(string left, string right) => $"{left} || {right}";
    public override string RenderTakePrefix(int count) => string.Empty;
    public override string RenderCurrentDateTime() => "CURRENT_TIMESTAMP";
    public override string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"DAYS_BETWEEN({startDate}, {endDate})";

    public override string RenderPaging(int? skip, int? take)
    {
        if (take is null && skip is null)
            return string.Empty;

        return $" LIMIT {(take?.ToString() ?? "2147483647")}" +
            (skip is null ? string.Empty : $" OFFSET {skip}");
    }
}
