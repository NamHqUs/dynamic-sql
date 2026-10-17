using Namh.Data.DynamicSql;

namespace Demo.ServiceContracts.Repositories;

public interface IFunctionDefinitionRepository
{
    IReadOnlyCollection<SqlFunctionDefintion> GetAll();
    void Save(SqlFunctionDefintion definition);
    void Delete(string functionName);
}
