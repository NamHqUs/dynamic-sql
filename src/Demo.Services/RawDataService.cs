using Demo.Contracts.Repositories;
using Demo.Contracts.Services;
using Demo.Contracts.Dto;

namespace Demo.Services;

public sealed class RawDataService(IRawDataRepository repository) : IRawDataService
{
    public string[] GetExposedTables() => repository.GetExposedTables();

    public DatabaseSchemaDto GetDatabaseSchema() => repository.GetDatabaseSchema();

    public TableDataDto GetTableData(string tableName, string? schema = null, int? limit = null)
        => repository.GetTableData(tableName, schema, limit);
}
