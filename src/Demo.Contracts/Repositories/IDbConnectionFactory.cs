using System.Data;

namespace Demo.Contracts.Repositories;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
    string[] GetTableNames();
    string QuoteIdentifier(string identifier);
}
