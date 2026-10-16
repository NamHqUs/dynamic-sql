using System.Text;

namespace Namh.Data.DynamicSql.Compilation.Expressions;

class TakeExpression : TableExpression
{
    public TakeExpression(TableExpression tableContext, int numberOfRecords)
        : base(tableContext)
    {
        TakeCount = numberOfRecords;
    }

    public override Type Type => typeof(int);

    protected override StringBuilder TranslateSelect()
        => new StringBuilder()
            .Append(base.TranslateSelect());
}
