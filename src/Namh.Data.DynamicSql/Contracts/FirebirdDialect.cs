using System.Data;
using System.Globalization;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public sealed class FirebirdDialect : ISqlDialect
{
    public string Key => "Firebird";

    public string QuoteIdentifier(string identifier)
        => $"\"{identifier.ToUpperInvariant().Replace("\"", "\"\"")}\"";

    public string RenderTable(Entity entity)
        => entity.IsRawSql
            ? RawSqlEntitySqlGuard.ValidateAndReturn(entity.DbTable, RawSqlEntityDialect.Firebird)
            : QuoteIdentifier(entity.DbTable ?? entity.Name);

    public string RenderBoolean(bool value) => value ? "TRUE" : "FALSE";
    public string RenderStringLiteral(string value) => $"'{value.Replace("'", "''")}'";
    public string RenderStringConcatenation(string left, string right) => $"{left} || {right}";
    public string RenderTakePrefix(int count) => $"FIRST {count} ";
    public string FormatParameterValue(object? value) => SqlDialect.FormatParameterValue(value);
    public object? NormalizeParameterValue(object? value)
        => value is decimal decimalValue ? Convert.ToDouble(decimalValue, CultureInfo.InvariantCulture) : value;
    public void ConfigureParameter(IDbDataParameter parameter, object? value)
    {
        var normalized = NormalizeParameterValue(value);
        if (normalized is double or float)
            parameter.DbType = DbType.Double;
        parameter.Value = normalized ?? DBNull.Value;
    }
    public string PrepareCommandText(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
    {
        foreach (var parameter in parameters.Where(parameter =>
            parameter.Value is decimal or double or float))
        {
            commandText = commandText.Replace(
                parameter.Name,
                $"CAST({parameter.Name} AS DOUBLE PRECISION)",
                StringComparison.Ordinal);
        }

        return commandText;
    }
    public string RenderCurrentDateTime() => "CURRENT_TIMESTAMP";
    public string RenderDateDifferenceInDays(string startDate, string endDate)
        => $"DATEDIFF(DAY FROM {startDate} TO {endDate})";

    public string RenderPaging(int? skip, int? take)
        => skip is null
            ? string.Empty
            : $" ROWS {skip + 1}" + (take is null ? string.Empty : $" TO {skip + take}");
}
