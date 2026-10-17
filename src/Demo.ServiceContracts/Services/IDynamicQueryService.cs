using Namh.Data.DynamicSql;

namespace Demo.ServiceContracts.Services;

public interface IDynamicQueryService
{
    string GetSqlQueryString(QueryDefinition query);
    Task<TableDataDto> ExecuteQueryAsync(QueryDefinition query, CancellationToken cancellationToken = default);
}
