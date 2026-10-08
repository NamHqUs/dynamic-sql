using System.Text;

namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

class TakeExpression(TableExpression tableContext, int numberOfRecords) : TableExpression(tableContext)
{
    public override Type Type => typeof(int);

    protected override StringBuilder TranslateSelect()
        => new StringBuilder($"Top {numberOfRecords} ").Append(base.TranslateSelect());
}
