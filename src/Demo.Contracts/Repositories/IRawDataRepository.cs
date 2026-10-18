namespace Demo.Contracts.Repositories;

public interface IRawDataRepository
{
    string[] GetExposedTables();
    TableDataDto GetTableData(string tableName);
}
