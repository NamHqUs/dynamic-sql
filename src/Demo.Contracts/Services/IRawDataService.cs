namespace Demo.Contracts.Services;

public interface IRawDataService
{
    string[] GetExposedTables();
    TableDataDto GetTableData(string tableName);
}
