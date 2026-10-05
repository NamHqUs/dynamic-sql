using System.Collections;
using System.Linq.Expressions;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public interface IDbQueryable : IEnumerable
{
    Expression Expression { get; }

    IDbQueryProvider Provider { get; }
}
