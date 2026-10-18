using Namh.Data.DynamicSql;

namespace Demo.Contracts.Repositories;

public interface IFunctionDefinitionRepository
{
    IReadOnlyCollection<SqlFunctionDefintion> GetAll();
    bool IsBuiltin(string functionName);
    void Save(SqlFunctionDefintion definition);
    void Delete(string functionName);
}
