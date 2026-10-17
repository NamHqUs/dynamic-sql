using Demo.ServiceContracts.Dto;

namespace Demo.ServiceContracts.Repositories;

public interface IRawDataRepository
{
    string[] GetExposedTables();
    TableDataDto GetTableData(string tableName);
}
