using System.Linq.Expressions;

#pragma warning disable IDE0130
namespace Namh.Data.DynamicSql;
#pragma warning restore IDE0130

public interface IDbQueryProvider
{
    IDbQueryable CreateQuery(Expression expression);
    SqlQuery GetQuery(Expression expression);
    IEnumerable<DataRecord> Execute(Expression expression);
    object? ExecuteScalar(Expression expression);
    Task<List<DataRecord>> ExecuteAsync(Expression expression, CancellationToken cancellationToken);
    Task<object?> ExecuteScalarAsync(Expression expression, CancellationToken cancellationToken);
}
