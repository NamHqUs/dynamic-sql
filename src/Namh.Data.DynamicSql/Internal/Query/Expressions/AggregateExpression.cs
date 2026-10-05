using System.Text;

namespace Namh.Data.DynamicSql.Internal.Query.Expressions
{
    class AggregateExpression : TableExpression
    {
        public override Type Type => typeof(double);

        public AggregateExpression(TableExpression tableContext, string aggregation) : base(tableContext)
        {
            aggregation = aggregation.Replace("@", $"{tableContext.Alias}.");
            var i = aggregation.IndexOf('$');
            SqlSelect = new StringBuilder(i < 0 ? aggregation : aggregation[..i]);

            if (i > 0)
            {
                SqlWhere ??= new StringBuilder();
                SqlWhere.Append(SqlWhere.Length == 0 ? "" : " And ").Append(aggregation.AsSpan(i + 1));
            }
        }

        protected override StringBuilder TranslateSelect()
            => SqlSelect ?? base.TranslateSelect();
    }
}
