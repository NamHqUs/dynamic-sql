using Demo.Contracts.Repositories;
using Demo.Contracts.Services;

namespace Demo.Services;

public sealed class RawDataService(IRawDataRepository repository) : IRawDataService
{
    public string[] GetExposedTables() => repository.GetExposedTables();

    public TableDataDto GetTableData(string tableName) => repository.GetTableData(tableName);
}
