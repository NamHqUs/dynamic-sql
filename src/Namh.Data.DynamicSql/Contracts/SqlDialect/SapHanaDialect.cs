#pragma warning disable IDE0130
using System.Data;
using System.Globalization;

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
        => $"\"{identifier.ToUpperInvariant().Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

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
    public override object? NormalizeParameterValue(object? value)
        => value is decimal decimalValue ? Convert.ToDouble(decimalValue, CultureInfo.InvariantCulture) : value;

    public override void ConfigureParameter(IDbDataParameter parameter, object? value)
    {
        parameter.ParameterName = parameter.ParameterName.Replace("@", ":", StringComparison.Ordinal);
        var normalized = NormalizeParameterValue(value);
        if (normalized is int or long)
            parameter.DbType = DbType.Int32;
        else if (normalized is double or float or decimal)
            parameter.DbType = DbType.Double;
        else if (normalized is DateTime or DateTimeOffset)
            parameter.DbType = DbType.DateTime;
        parameter.Value = normalized ?? DBNull.Value;
    }

    public override string PrepareCommandText(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
    {
        var prepared = parameters.Aggregate(
            commandText,
            (text, parameter) => text.Replace(
                parameter.Name,
                parameter.Value is string value
                    ? RenderStringLiteral(value)
                    : parameter.Name.Replace("@", ":", StringComparison.Ordinal),
                StringComparison.Ordinal));

        var groupBy = prepared.IndexOf("GROUP BY", StringComparison.OrdinalIgnoreCase);
        var orderBy = prepared.IndexOf("ORDER BY", groupBy, StringComparison.OrdinalIgnoreCase);
        if (groupBy >= 0 && orderBy > groupBy)
        {
            var groupClause = prepared[groupBy..orderBy];
            var aggregateStart = groupClause.IndexOf(", ROUND (t1.", StringComparison.OrdinalIgnoreCase);
            if (aggregateStart >= 0)
            {
                var aggregateEnd = groupClause.IndexOf("))", aggregateStart, StringComparison.Ordinal);
                if (aggregateEnd >= 0)
                    groupClause = groupClause.Remove(aggregateStart, aggregateEnd + 2 - aggregateStart);
            }
            prepared = prepared[..groupBy] + groupClause + prepared[orderBy..];
        }

        return prepared;
    }

    public override string RenderPaging(int? skip, int? take)
    {
        if (take is null && skip is null)
            return string.Empty;

        return $" LIMIT {(take?.ToString() ?? "2147483647")}" +
            (skip is null ? string.Empty : $" OFFSET {skip}");
    }
}
