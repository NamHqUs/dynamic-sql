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
    object? NormalizeParameterValue(object? value) => value;
    string PrepareCommandText(
        string commandText,
        IReadOnlyList<(string Name, object? Value)> parameters)
        => commandText;
    void ConfigureParameter(IDbDataParameter parameter, object? value)
    {
        parameter.Value = NormalizeParameterValue(value) ?? DBNull.Value;
    }
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
