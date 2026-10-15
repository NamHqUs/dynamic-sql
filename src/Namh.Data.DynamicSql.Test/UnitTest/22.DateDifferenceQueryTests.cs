using Microsoft.Data.Sqlite;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal sealed class _22_DateDifferenceQueryTests
{
    private static readonly Entity[] Metadata =
    [
        new(
            Name: "Customers",
            Fields:
            [
                new("Id", DataType.Number),
                new("Name"),
                new("DeletedOn", DataType.Datetime)
            ]),
        new(
            Name: "Invoices",
            Fields:
            [
                new("Id", DataType.Number),
                new("CustomerId", DataType.Number),
                new("InvoiceNumber"),
                new("InvoiceDate", DataType.Datetime),
                new("DueDate", DataType.Datetime),
                new("DeletedOn", DataType.Datetime)
            ],
            Relations:
            [
                new(
                    Name: "Customer",
                    RelatedEntity: "Customers",
                    Type: RelationType.Lookup,
                    RelatedKeys: [new("CustomerId", "Id")])
            ])
    ];

    private static QueryDto CreateQuery()
        => new()
        {
            Entity = "Invoices",
            Fields =
            [
                new(new FieldDto("InvoiceNumber")),
                new(new FieldDto("Customer.Name")),
                new(new FieldDto("InvoiceDate")),
                new(
                    new FunctionFieldDto("DateSub",
                    [
                        new FieldDto("DueDate"),
                        new FunctionFieldDto("GETDATE", [])
                    ]),
                    "DaysOverdue")
            ],
            Page = 1,
            PageSize = 50
        };

    [Test]
    public async Task DateDifferenceProjectionExecutesOnSqlite()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE "Customers" (
                    "Id" INTEGER PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "DeletedOn" TEXT NULL
                );
                CREATE TABLE "Invoices" (
                    "Id" INTEGER PRIMARY KEY,
                    "CustomerId" INTEGER NOT NULL,
                    "InvoiceNumber" TEXT NOT NULL,
                    "InvoiceDate" TEXT NOT NULL,
                    "DueDate" TEXT NOT NULL,
                    "DeletedOn" TEXT NULL
                );
                INSERT INTO "Customers" ("Id", "Name", "DeletedOn")
                VALUES (1, 'Ada', NULL);
                INSERT INTO "Invoices"
                    ("Id", "CustomerId", "InvoiceNumber", "InvoiceDate", "DueDate", "DeletedOn")
                VALUES
                    (1, 1, 'INV-001', date('now', 'localtime'), date('now', 'localtime', '-3 days'), NULL);
                """;
            command.ExecuteNonQuery();
        }

        using var context = new DynamicSqlContext(connection, Metadata, new SqliteDialect());
        var queryResult = await context.Query(CreateQuery()).ToListWithTotalRecordsAsync();
        var record = queryResult.Records.Single();

        Assert.That(queryResult.TotalRecords, Is.EqualTo(1));
        Assert.That(record["InvoiceNumber"], Is.EqualTo("INV-001"));
        Assert.That(record["Customer.Name"], Is.EqualTo("Ada"));
        Assert.That(Convert.ToInt32(record["DaysOverdue"]), Is.EqualTo(3));
    }

    [Test]
    public void DateDifferenceProjectionUsesProviderSpecificSql()
    {
        using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        using var sqliteContext = new DynamicSqlContext(sqliteConnection, Metadata, new SqliteDialect());
        var sqliteSql = sqliteContext.Query(CreateQuery()).ToSql().CommandText;
        Assert.That(sqliteSql, Does.Match(@"julianday\s*\(\s*date"));
        Assert.That(sqliteSql, Does.Match(@"datetime\s*\(\s*'now',\s*'localtime'\s*\)"));

        using var sqlServerConnection = new SqliteConnection("Data Source=:memory:");
        using var sqlServerContext = new DynamicSqlContext(sqlServerConnection, Metadata, new SqlServerDialect());
        var sqlServerSql = sqlServerContext.Query(CreateQuery()).ToSql().CommandText;
        Assert.That(sqlServerSql, Does.Match(@"DATEDIFF\s*\(\s*day,"));
        Assert.That(sqlServerSql, Does.Match(@"GETDATE\s*\(\s*\)"));

        using var postgresConnection = new SqliteConnection("Data Source=:memory:");
        using var postgresContext = new DynamicSqlContext(postgresConnection, Metadata, new PostgresDialect());
        var postgresSql = postgresContext.Query(CreateQuery()).ToSql().CommandText;
        Assert.That(postgresSql, Does.Match(@"CAST\s*\(.* AS DATE\)\s*-\s*CAST\s*\("));
        Assert.That(postgresSql, Does.Contain("CURRENT_TIMESTAMP"));
    }
}
