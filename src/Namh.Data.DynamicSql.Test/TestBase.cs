using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System.Data;

namespace Namh.Data.DynamicSql.Test;

[TestFixture]
public class TestBase 
{
    protected DynamicSqlContext _db = null!;
    private IDbConnection _connection = null!;

    [OneTimeSetUp]
    public void SetupOnce()
    {
        _connection = DefinedData.GetDbConnection(DbServerType.SqlServer);

        IMetaProvider metaProvider = new MetaProvider(DefinedMetadata.Metadata);
        _db = new DynamicSqlContext(new Lazy<IDbConnection>(() => _connection), metaProvider);
    }


    [SetUp]
    public void Setup() { }

    [TearDown]
    public void TearDown()  { }

    [OneTimeTearDown]
    public void TearDownOnce()
    {
        _db.Dispose();
        _connection?.Dispose();
    }
}