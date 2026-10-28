using Demo.Contracts.Dto;

namespace Demo.Contracts.Repositories;

public interface IRawDataRepository
{
    string[] GetExposedTables();
    DatabaseSchemaDto GetDatabaseSchema();
    TableDataDto GetTableData(string tableName, string? schema = null, int? limit = null);
}
