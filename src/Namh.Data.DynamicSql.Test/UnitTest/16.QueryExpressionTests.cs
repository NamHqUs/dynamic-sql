using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public sealed class _16_QueryExpressionTests
{
    private static readonly Entity[] Metadata =
    [
        new(
            Name: "Products",
            Fields:
            [
                new("Id", DataType.Number),
                new("Name"),
                new("CostPrice", DataType.Number),
                new("DeletedOn", DataType.Datetime)
            ],
            Relations:
            [
                new(
                    Name: "InvoiceItems",
                    RelatedEntity: "InvoiceItems",
                    Type: RelationType.Collection,
                    RelatedKeys: [new("Id", "ProductId")])
            ]),
        new(
            Name: "InvoiceItems",
            Fields:
            [
                new("Id", DataType.Number),
                new("ProductId", DataType.Number),
                new("InvoiceId", DataType.Number),
                new("Quantity", DataType.Number),
                new("LineTotal", DataType.Number),
                new("DeletedOn", DataType.Datetime)
            ],
            Relations:
            [
                new(
                    Name: "Invoice",
                    RelatedEntity: "Invoices",
                    Type: RelationType.Lookup,
                    RelatedKeys: [new("InvoiceId", "Id")])
            ]),
        new(
            Name: "Invoices",
            Fields:
            [
                new("Id", DataType.Number),
                new("Status"),
                new("DeletedOn", DataType.Datetime)
            ])
    ];

    [Test]
    public async Task JsonDefinitionBuildsCalculatedAggregateProjectionWithFluentGrouping()
    {
        const string json = """
            {
              "entity": "Products",
              "fields": [
                { "field": { "kind": "field", "field": "Name" } },
                {
                  "alias": "UnitsSold",
                  "field": {
                    "kind": "function",
                    "function": "SUM",
                    "arguments": [{ "kind": "field", "field": "InvoiceItems.Quantity" }]
                  }
                },
                {
                  "alias": "GrossProfit",
                  "field": {
                    "kind": "function",
                    "function": "SUM",
                    "arguments": [{
                      "kind": "binary",
                      "operator": "-",
                      "arguments": [
                        { "kind": "field", "field": "InvoiceItems.LineTotal" },
                        {
                          "kind": "binary",
                          "operator": "*",
                          "arguments": [
                            { "kind": "field", "field": "InvoiceItems.Quantity" },
                            { "kind": "field", "field": "CostPrice" }
                          ]
                        }
                      ]
                    }]
                  }
                },
                {
                  "alias": "MarginPercent",
                  "field": {
                    "kind": "function",
                    "function": "ROUND",
                    "arguments": [
                      {
                        "kind": "binary",
                        "operator": "/",
                        "arguments": [
                          {
                            "kind": "binary",
                            "operator": "*",
                            "arguments": [
                              {
                                "kind": "function",
                                "function": "SUM",
                                "arguments": [{
                                  "kind": "binary",
                                  "operator": "-",
                                  "arguments": [
                                    { "kind": "field", "field": "InvoiceItems.LineTotal" },
                                    {
                                      "kind": "binary",
                                      "operator": "*",
                                      "arguments": [
                                        { "kind": "field", "field": "InvoiceItems.Quantity" },
                                        { "kind": "field", "field": "CostPrice" }
                                      ]
                                    }
                                  ]
                                }]
                              },
                              { "kind": "value", "value": 100 }
                            ]
                          },
                          {
                            "kind": "function",
                            "function": "NULLIF",
                            "arguments": [
                              {
                                "kind": "function",
                                "function": "SUM",
                                "arguments": [{ "kind": "field", "field": "InvoiceItems.LineTotal" }]
                              },
                              { "kind": "value", "value": 0 }
                            ]
                          }
                        ]
                      },
                      { "kind": "value", "value": 2 }
                    ]
                  }
                }
              ],
              "includeArchive": false,
              "filter": {
                "logicalOperation": 0,
                "filters": [{
                  "logicalOperator": 0,
                  "field": "InvoiceItems.Invoice.Status",
                  "operation": 0,
                  "value": "Paid",
                  "parameterized": false
                }]
              },
              "sorts": [{ "field": "GrossProfit", "direction": 1 }]
            }
            """;

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE "Products" (
                    "Id" INTEGER PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "CostPrice" NUMERIC NOT NULL,
                    "DeletedOn" TEXT NULL
                );
                CREATE TABLE "InvoiceItems" (
                    "Id" INTEGER PRIMARY KEY,
                    "ProductId" INTEGER NOT NULL,
                    "InvoiceId" INTEGER NOT NULL,
                    "Quantity" NUMERIC NOT NULL,
                    "LineTotal" NUMERIC NOT NULL,
                    "DeletedOn" TEXT NULL
                );
                CREATE TABLE "Invoices" (
                    "Id" INTEGER PRIMARY KEY,
                    "Status" TEXT NOT NULL,
                    "DeletedOn" TEXT NULL
                );
                INSERT INTO "Products" ("Id", "Name", "CostPrice", "DeletedOn") VALUES
                    (1, 'Widget', 3, NULL),
                    (2, 'No sales', 4, NULL);
                INSERT INTO "Invoices" ("Id", "Status", "DeletedOn") VALUES
                    (10, 'Paid', NULL),
                    (11, 'Draft', NULL);
                INSERT INTO "InvoiceItems" ("Id", "ProductId", "InvoiceId", "Quantity", "LineTotal", "DeletedOn") VALUES
                    (100, 1, 10, 2, 20, NULL),
                    (101, 1, 10, 1, 12, NULL),
                    (102, 1, 11, 99, 1000, NULL);
                """;
            command.ExecuteNonQuery();
        }
        using var db = new DynamicSqlContext(connection, Metadata);

        var definition = JsonSerializer.Deserialize<QueryDto>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The query request JSON did not produce a definition.");
        var query = db.Query(definition).GroupBy("Id", "Name");
        var sql = query.ToSql();

        Assert.That(sql.CommandText, Does.Match(@"SUM\s*\(\s*\(t1\.""LineTotal"" - \(t1\.""Quantity"" \* t0\.""CostPrice""\)\)\)"));
        Assert.That(sql.CommandText, Does.Match(@"NULLIF\s*\(\s*SUM\s*\(t1\.""LineTotal""\), @p"));
        Assert.That(sql.CommandText, Does.Contain("GROUP BY t0.\"Id\", t0.\"Name\""));
        Assert.That(sql.CommandText, Does.Match(@"ORDER BY SUM\s*\("));
        Assert.That(sql.CommandText, Does.Match(@"ORDER BY[\s\S]*DESC"));
        Assert.That(sql.Parameters.Select(parameter => parameter.Value),
            Is.EquivalentTo(new object?[] { 100m, 0m, 2m }));
        Assert.That(definition.ToString(), Does.Not.Contain("GroupBy"));
        Assert.That(definition.ToString(), Does.Contain("ROUND("));

        var queryResult = await query.ToListWithTotalRecordsAsync();
        var records = queryResult.Records;

        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(queryResult.TotalRecords, Is.EqualTo(1));
        Assert.That(records[0]["Name"], Is.EqualTo("Widget"));
        Assert.That(Convert.ToDecimal(records[0]["UnitsSold"]), Is.EqualTo(3m));
        Assert.That(Convert.ToDecimal(records[0]["GrossProfit"]), Is.EqualTo(23m));
        Assert.That(Convert.ToDecimal(records[0]["MarginPercent"]), Is.EqualTo(71.88m).Within(0.01m));
    }

    [Test]
    public void QueryExpressionRejectsUnsupportedFunctions()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var db = new DynamicSqlContext(connection, Metadata);
        var request = new QueryDto
        {
            Entity = "Products",
            Fields =
            [
                new ProjectionDto(
                    new FunctionFieldDto(
                        "RANDOM_SQL",
                        [new ConstFieldDto(1)]),
                    "Unsafe")
            ]
        };

        Assert.Throws<FormatException>(() => db.Query(request).ToSql());
    }
}
