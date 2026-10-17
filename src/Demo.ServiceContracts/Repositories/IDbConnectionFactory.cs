using System.Data;

namespace Demo.ServiceContracts.Repositories;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
