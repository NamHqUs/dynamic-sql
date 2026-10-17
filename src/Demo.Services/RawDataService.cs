using Demo.ServiceContracts.Dto;
using Demo.ServiceContracts.Repositories;
using Demo.ServiceContracts.Services;

namespace Demo.Services;

public sealed class RawDataService(IRawDataRepository repository) : IRawDataService
{
    public string[] GetExposedTables() => repository.GetExposedTables();

    public TableDataDto GetTableData(string tableName) => repository.GetTableData(tableName);
}
