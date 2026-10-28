using Demo.Contracts.Dto;

namespace Demo.Contracts.Services;

public interface IRawDataService
{
    string[] GetExposedTables();
    DatabaseSchemaDto GetDatabaseSchema();
    TableDataDto GetTableData(string tableName, string? schema = null, int? limit = null);
}
