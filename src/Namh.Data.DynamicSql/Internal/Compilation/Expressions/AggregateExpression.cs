using System.Text;
namespace Namh.Data.DynamicSql.Internal.Compilation.Expressions;

class AggregateExpression : TableExpression
{
    public override Type Type => typeof(double);

    public AggregateExpression(TableExpression tableContext, string aggregation) : base(tableContext)
    {
        ProjectionItems = [];
        aggregation = aggregation.Replace("@", $"{tableContext.Alias}.");
        var i = aggregation.IndexOf('$');
        SqlSelect = new StringBuilder(i < 0 ? aggregation : aggregation[..i]);

        if (i > 0)
            SqlWhere.Add(new RawSqlExpression(aggregation[(i + 1)..], typeof(bool)));
    }

    protected override StringBuilder TranslateSelect()
        => SqlSelect ?? base.TranslateSelect();

    internal string SelectSql => SqlSelect!.ToString();
}
