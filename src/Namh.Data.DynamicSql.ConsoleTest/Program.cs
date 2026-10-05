
using Microsoft.Data.SqlClient;
using System.Reflection.Metadata;

// Sqlite
//using var connection = new SqliteConnection("Data Source=:memory:");
//connection.Open();
//DefinedData.SeedData(connection);

// SqlServer
using var connection = new SqlConnection("Server=localhost;Database=DynamicSqlTest;User Id=sa;Password=NoPassword123!;TrustServerCertificate=True;");
//connection.Open();
//DefinedData.SeedData(connection);

IMetaProvider metaProvider = new MetaProvider(DefinedMetadata.Metadata);
using var db = new DynamicSqlContext(new Lazy<IDbConnection>(() => connection), metaProvider);

var query = db.Query("User")
           .Select(e => new DataRecord
               {
                    { "Id", "Id" },
                    { "Manager-Name", e["Name"] + "." + e["Manager.Name"] },
               });
var data = query.ToList();

string[] expectedValues = [
    "NULL",
            "Bob.Alice",
            "Charlie.Alice",
            "David.Bob",
            "Eve.David",
            "Frank.David"];
var actualValues = data.Select(e => e["Manager-Name"] ?? "NULL").ToArray();
