namespace Namh.Data.DynamicSql.Test.UnitTest
{
    [TestFixture]
    public class ArchiveTest : TestBase
    {
        [Test]
        public void Simple()
        {
            var query = _db.Query("User").Select("Id");

            var queryString = query.ToString();
            var data = query.Count();
            Assert.AreEqual(data, 6);
        }

        [Test]
        public void IncludeArchive()
        {
            var query = _db.Query("User", includeArchive: true).Select("Id");

            var queryString = query.ToString();
            var data = query.Count();
            Assert.AreEqual(data, 7);
        }

        [Test]
        public void CollectionArchive()
        {
            var query = _db.Query("School")
                .Select("Name", "Students.Name");

            var queryString = query.ToString();
            var data = query.ToList();
            Assert.AreEqual(data.Count, 5);
        }

        [Test]
        public void MultipleCollectionArchive()
        {
            var query = _db.Query("School")
                .Select("Name", "Students.Name", "Teachers.Name");

            var queryString = query.ToString();
            var data = query.ToList();
            Assert.AreEqual(data.Count, 8);

        }

        [Test]
        public void FilterArchive()
        {
            var query = _db.Query("User")
                .Where(e => e["Manager.Name"] == "Alan")
                .Where(e => e.Query("Members").Any(x => x["Name"] == e["Name"]))
                .Where(e => e.Query("MySchool.Teachers").Count() >= 1)
                .Select("Name", "Manager.Name", "MySchool.Name");

            var queryString = query.ToString();
            var data = query.ToList();
            Assert.AreEqual(data.Count, 1);
        }

        [Test]
        public void FilterIncludeArchive()
        {
            var query = _db.Query("User", includeArchive: true)
                .Where(e => e["Manager.Name"] == "Alan")
                .Where(e => e.Query("Members").Any(x => x["Name"] == e["Name"]))
                .Where(e => e.Query("MySchool.Teachers").Count() >= 2)
                .Select("Name", "Manager.Name", "MySchool.Name");

            var queryString = query.ToString();
            var data = query.ToList();
            Assert.AreEqual(data.Count, 1);
        }
    }
}
