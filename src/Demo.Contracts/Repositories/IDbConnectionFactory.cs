using System.Data;
using Demo.Contracts.Dto;

namespace Demo.Contracts.Repositories;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
    string[] GetTableNames();
    string QuoteIdentifier(string identifier);
    DatabaseSchemaDto GetDatabaseSchema();
}
