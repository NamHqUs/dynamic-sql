namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public class ComputedTest : TestBase
{
    [Test]
    public void SimpleProjection()
    {
        var query = _db.Query("User")
            .Select(e => new DataRecord
            {
                //{"Func", e.RawSql("DATEADD(month, 1, birthday)")},
                {"Computed", e.Query("Members").Count() + 1 }
            });

        var queryString = query.ToString();
        var data = query.ToList();
    }
}
