namespace Namh.Data.DynamicSql.Internal.Compilation.Expressions;

internal sealed class DateDifferenceInDaysSqlExpression(
    SqlExpression startDate,
    SqlExpression endDate) : SqlExpression(typeof(int))
{
    public SqlExpression StartDate { get; } = startDate;
    public SqlExpression EndDate { get; } = endDate;
}
