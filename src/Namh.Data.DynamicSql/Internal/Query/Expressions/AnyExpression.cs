namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

class AnyExpression : TableExpression
{
    private readonly TableExpression _tableContext;

    public override Type Type => typeof(bool);

    internal AnyExpression(TableExpression tableContext): base(tableContext.Entity, tableContext.Alias)
    {
        _tableContext = tableContext;
    }

    protected override string ToSql()
    {
        return $"(SELECT 1 WHERE EXISTS(" +
            $"SELECT 1 " +
            $"FROM {_tableContext.SqlFrom}" +
            (_tableContext.SqlWhere == null ? "" : $" WHERE {_tableContext.SqlWhere}") +
            $")) = 1";
    }
}
