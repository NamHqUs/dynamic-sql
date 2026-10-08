using System.Text;

namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

class TakeExpression : TableExpression
{
    private readonly int _numberOfRecords;

    public TakeExpression(TableExpression tableContext, int numberOfRecords)
        : base(tableContext)
    {
        _numberOfRecords = numberOfRecords;
        TakeCount = numberOfRecords;
    }

    public override Type Type => typeof(int);

    protected override StringBuilder TranslateSelect()
        => new StringBuilder(SkipCount.HasValue ? string.Empty : SqlDialect.RenderTakePrefix(_numberOfRecords))
            .Append(base.TranslateSelect());
}
