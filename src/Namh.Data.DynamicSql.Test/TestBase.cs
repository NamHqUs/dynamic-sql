using System.Data;

namespace Namh.Data.DynamicSql.Test;

[TestFixture]
public class TestBase 
{
    protected DynamicSqlContext _db = null!;
    protected ISqlDialect _sqlDialect = null!;
    private IDbConnection _connection = null!;

    [OneTimeSetUp]
    public void SetupOnce()
    {
        _connection = DefinedData.GetDbConnection(DbServerType.SqlServer);
        _sqlDialect = new SqlServerDialect();

        IMetaProvider metaProvider = new MetaProvider(DefinedMetadata.Metadata);
        _db = new DynamicSqlContext(_connection, metaProvider, _sqlDialect);
    }


    [SetUp]
    public void Setup() { }

    [TearDown]
    public void TearDown()  { }

    [OneTimeTearDown]
    public void TearDownOnce()
    {
        _db.Dispose();
    }
}