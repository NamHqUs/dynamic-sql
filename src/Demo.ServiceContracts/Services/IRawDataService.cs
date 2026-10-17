namespace Demo.ServiceContracts.Services;

public interface IRawDataService
{
    string[] GetExposedTables();
    TableDataDto GetTableData(string tableName);
}
