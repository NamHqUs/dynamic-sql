using Namh.Data.DynamicSql;

namespace Demo.ServiceContracts.Repositories;

public interface IFunctionDefinitionRepository
{
    IReadOnlyList<SqlFunctionDefintion> GetBuiltInDefinitions();
    IReadOnlyList<SqlFunctionDefintion> GetCustomDefinitions();
    void Save(SqlFunctionDefintion definition);
    void Delete(string functionName);
}
