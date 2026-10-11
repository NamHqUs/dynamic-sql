using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Namh.Data.DynamicSql.Internal.Query;

class DbQueryable : IDbQueryable, IEnumerable<DataRecord>, IEnumerable
{
    public readonly DbQueryProvider Provider;
    private int _toStringDepth;

    IDbQueryProvider IDbQueryable.Provider => Provider;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public Expression Expression { get; }

    internal DbQueryable(DbQueryProvider provider, Expression? expression = null)
    {
        Provider = provider;
        Expression = expression ?? Expression.Constant(this);
    }

    public IEnumerator<DataRecord> GetEnumerator()
        => Provider.Execute(Expression).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => Provider.Execute(Expression).GetEnumerator();

    public override string ToString()
    {
        if (_toStringDepth > 0)
            return nameof(DbQueryable);

        try
        {
            _toStringDepth++;
            return Provider.GetQueryText(Expression);
        }
        finally
        {
            _toStringDepth--;
        }
    }
}
