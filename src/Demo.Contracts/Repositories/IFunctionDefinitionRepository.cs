using Namh.Data.DynamicSql;

namespace Demo.Contracts.Repositories;

public interface IFunctionDefinitionRepository
{
    IReadOnlyCollection<SqlFunctionDefintion> GetAll();
    IReadOnlyCollection<SqlFunctionDefintion> GetUserDefined();
    bool IsBuiltin(string functionName);
    void Save(SqlFunctionDefintion definition);
    void Delete(string functionName);
}
