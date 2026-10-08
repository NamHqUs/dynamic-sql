namespace Namh.Data.DynamicSql.Test.UnitTest;


[TestFixture]
public class _02_Filter_FieldTest : TestBase
{

    [Test]
    public void _01_Field_Primitive()
    {
        var query = _db.Query("User").Where(e => e["Age"] > 30);
        Assert.That(query.Count(), Is.EqualTo(3));
    }

    [Test]
    public void _02_Field_Lookup()
    {
        var query = _db.Query("User").Where(e => e["Manager.Name"] == "Alice");
        Assert.That(query.Count(), Is.EqualTo(2));
    }

    [Test]
    public void _02_Field_Lookup_Lookup()
    {
        var query = _db.Query("User").Where(e => e["Manager.Manager.Name"] == "Alice");
        Assert.That(query.Count(), Is.EqualTo(1));
    }

    [Test]
    public void _02_Field_Lookup_Collection()
    {
        var query = _db.Query("User").Where(e => e["Manager.Members.Name"] == "Bob");
        Assert.That(query.Count(), Is.EqualTo(2));
    }


    [Test]
    public void _03_Field_Collection()
    {
        var query = _db.Query("User").Where(e => e["Members.Name"] == "Bob");
        Assert.That(query.Count(), Is.EqualTo(1));
    }

    [Test]
    public void _03_Field_Collection_Count()
    {
        var query = _db.Query("User").Where(e => e.Query("Members").Count() >= 2);
        Assert.That(query.Count(), Is.EqualTo(2));
    }
    [Test]
    public void _03_Field_Collection_Any()
    {
        var query = _db.Query("User").Where(e => e.Query("Members").Any(x => x["Name"] == "Bob"));
        Assert.AreEqual(query.Count(), 1);
    }

    [Test]
    public void _03_Field_Collection_Lookup()
    {
        var query = _db.Query("User")
            .Select("Name", "Members.Name", "Members.Manager.Name")
            .Where(e => e["Members.Manager.Name"] == "Alice");
        var data= query.ToList();

        string[] expectedValues = [
            "Alice - Bob - Alice",
            "Alice - Charlie - Alice"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Members.Name"]} - {e["Members.Manager.Name"]}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));

        // Select only Name -> Duplicated records
        query = _db.Query("User")
            .Select("Name")
            .Where(e => e["Members.Manager.Name"] == "Alice");
        data = query.ToList();

        expectedValues = [
            "Alice",
            "Alice"];
        actualValues = [.. data.Select(e => $"{e["Name"]}")];
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void _03_Field_Collection_Lookup_SubQuery()
    {
        var query = _db.Query("User")
            .Select("Name", "Members.Name", "Members.Manager.Name")
            .Where(e => e.Query("Members").Any(x => x["Manager.Name"] == "Alice"));
        var data = query.ToList();

        string[] expectedValues = [
            "Alice - Bob - Alice",
            "Alice - Charlie - Alice"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Members.Name"]} - {e["Members.Manager.Name"]}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));

        // Select only Name -> Different from ...Where(e => e["Members.Manager.Name"] == "Alice");
        query = _db.Query("User")
           .Select("Name") 
           .Where(e => e.Query("Members").Any(x => x["Manager.Name"] == "Alice"));
        data = query.ToList();

        expectedValues = ["Alice"];  // ONLY 1 record
        actualValues = [.. data.Select(e => $"{e["Name"]}")];
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void _03_Field_Collection_Collection()
    {
        var query = _db.Query("User").Where(e => e["Members.Members.Name"] == "David");
        Assert.That(query.Count(), Is.EqualTo(1));
    }

    [Test]
    public void _03_Field_Collection_Collection_SubQuery()
    {
        var query = _db.Query("User")
            .Where(e => e.Query("Members").Any(x => x.Query("Members").Any(y => y["Name"] == "David")));

        Assert.That(query.Count(), Is.EqualTo(1));
    }

    [Test]
    public void _04_Field_Aggregate()
    {
        //"Aggregate Field must be follow collection field."

        var query = _db.Query("User").Where(e => e["Members.TotalMembers"] >= 2);
        Assert.AreEqual(query.Count(), 2);
    }

    [Test]
    public void _21_RawSql_DbColumn()
    {
        var query = _db.Query("User").Where(e => e.DbColumn("Id") > 1);
        Assert.That(query.Count(), Is.EqualTo(5));
    }

    [Test]
    public void _21_RawSql_Statement()
    {
        var rawQuery = $"Select Count(*) From {_sqlDialect.RenderTable(new Entity("User", []))} " +
            $"Where {_sqlDialect.QuoteIdentifier("ManagerId")} = t0.{_sqlDialect.QuoteIdentifier("Id")}";
        var query = _db.Query("user")
           .Where(e => e.RawSql(rawQuery) >= 2 && e["IsMale"] == true);
        Assert.That(query.Count(), Is.EqualTo(1));
    }
}
