using Microsoft.Data.Sqlite;
using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public class _50_QueryContextDefintion : TestBase
{
    [Test]
    public void Sample_RendersCteProjectionAndLeftJoin()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity(
                "User",
                [
                    new("Id", DataType.Number),
                    new("Name"),
                    new("ManagerId", DataType.Number),
                ]));

        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    Name: "UserContext",
                    Query: new QueryDefinition
                    {
                        Entity = "User",
                        Projection =
                        [
                            new("Id"),
                            new("Name", "Alias Name"),
                        ],
                    },
                    Condition: new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")))
            ],
            Projection =
            [
                new("Id"),
                new("Name", "User Name"),
                new("UserContext.Alias Name", "Manager Name")
            ],
        };

        var sql = db.Query(query).ToSql();

        Assert.That(
            sql.CommandText,
            Is.EqualTo(
                string.Join(
                    Environment.NewLine,
                    "WITH [UserContext] AS (",
                    "    SELECT t2.[Id] [Id], t2.[Name] [Alias Name]",
                    "    FROM dbo.[User] t2)",
                    "SELECT t0.[Id] [Id], t0.[Name] [User Name], t1.[Alias Name] [Manager Name]",
                    "FROM dbo.[User] t0",
                    "\tLEFT JOIN [UserContext] t1 ON (t0.[ManagerId] = t1.[Id])")));
        Assert.That(sql.Parameters, Is.Empty);
    }

    [Test]
    public void Contexts_CanUseCteAsRootAndBuildRangeJoin()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity(
                "Employees",
                [
                    new("Id", DataType.Number),
                    new("Salary", DataType.Number),
                ]),
            new Entity(
                "SalaryRules",
                [
                    new("Id", DataType.Number),
                    new("MinSalary", DataType.Number),
                ]));

        var query = new QueryDefinition
        {
            Entity = "A",
            Contexts =
            [
                new QueryContextDefintion(
                    "A",
                    new QueryDefinition
                    {
                        Entity = "Employees",
                        Projection = [new("Id"), new("Salary")]
                    }),
                new QueryContextDefintion(
                    "B",
                    new QueryDefinition
                    {
                        Entity = "SalaryRules",
                        Projection = [new("Id"), new("MinSalary")]
                    },
                    new ConditionStatementDefinition(
                        new FieldDefinition("A.Salary"),
                        BinaryOperator.Between,
                        new GroupFieldDefintion(
                        [
                            new FieldDefinition("B.MinSalary"),
                            new GroupFieldDefintion(
                            [
                                new FieldDefinition("B.MinSalary"),
                                new ConstFieldDefinition(MatchOperation.Add, 10000)
                            ])
                        ])),
                    QueryJoinType.Inner)
            ],
            Projection = [new("A.Salary"), new("B.MinSalary")]
        };

        var sql = db.Query(query).ToSql();

        Assert.That(sql.CommandText, Does.Contain("WITH [A] AS"));
        Assert.That(sql.CommandText, Does.Contain("[B] AS"));
        Assert.That(sql.CommandText, Does.Contain("FROM [A] t0"));
        Assert.That(sql.CommandText, Does.Contain("INNER JOIN [B] t1"));
        Assert.That(sql.CommandText, Does.Contain("t0.[Salary] BETWEEN t1.[MinSalary]"));
        Assert.That(sql.CommandText, Does.Contain("BETWEEN t1.[MinSalary] AND (t1.[MinSalary] + @p0)"));
        Assert.That(sql.Parameters.Select(parameter => parameter.Value), Is.EqualTo(new object?[] { 10000 }));
    }

    [TestCase("SqlServer")]
    [TestCase("Sqlite")]
    [TestCase("Postgres")]
    public void Contexts_UseDialectIdentifierQuoting(string dialectName)
    {
        ISqlDialect dialect = dialectName switch
        {
            "SqlServer" => new SqlServerDialect(),
            "Sqlite" => new SqliteDialect(),
            "Postgres" => new PostgresDialect(),
            _ => throw new ArgumentOutOfRangeException(nameof(dialectName))
        };
        using var db = CreateContext(
            dialect,
            new Entity("User", [new("Id", DataType.Number), new("ManagerId", DataType.Number)]));
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "UserContext",
                    new QueryDefinition { Entity = "User", Projection = [new("Id")] },
                    new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")))
            ],
            Projection = [new("UserContext.Id")]
        };

        var sql = db.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.Contain($"{dialect.QuoteIdentifier("UserContext")} AS"));
        Assert.That(sql, Does.Contain($"{dialect.QuoteIdentifier("UserContext")} t1"));
    }

    [Test]
    public void QueryDefinitionYaml_RoundTripsContextsAndJoinCondition()
    {
        const string yaml = """
            Entity: User
            Projection:
              - UserContext.Id | Manager Id
            Contexts:
              - Name: UserContext
                JoinType: Left
                Query:
                  Entity: User
                  Projection:
                    - Id
                Condition: ManagerId = Id
            """;

        var definition = QueryDefinition.FromYml(yaml);
        var parsedAgain = QueryDefinition.FromYml(definition.ToYml());

        Assert.That(parsedAgain.Contexts, Has.Count.EqualTo(1));
        Assert.That(parsedAgain.Contexts![0].Name, Is.EqualTo("UserContext"));
        Assert.That(parsedAgain.Contexts[0].JoinType, Is.EqualTo(QueryJoinType.Left));
        Assert.That(parsedAgain.Contexts[0].Condition?.ToText(), Is.EqualTo("ManagerId = Id"));
        Assert.That(parsedAgain.Projection[0].Alias, Is.EqualTo("Manager Id"));
    }

    [Test]
    public void QueryDefinitionYaml_ParsesAndRoundTripsRangeJoinExpressions()
    {
        const string yaml = """
            Entity: A
            Projection:
              - A.Salary
            Contexts:
              - Name: A
                Query:
                  Entity: Employees
                  Projection:
                    - Salary
              - Name: B
                JoinType: Inner
                Query:
                  Entity: SalaryRules
                  Projection:
                    - MinSalary
                Condition: A.Salary BETWEEN <B.MinSalary, B.MinSalary + 10000>
            """;
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("Employees", [new("Salary", DataType.Number)]),
            new Entity("SalaryRules", [new("MinSalary", DataType.Number)]));

        var definition = QueryDefinition.FromYml(yaml);
        var roundTripped = QueryDefinition.FromYml(definition.ToYml());
        var sql = db.Query(roundTripped).ToSql();

        Assert.That(sql.CommandText, Does.Contain("INNER JOIN [B]"));
        Assert.That(sql.CommandText, Does.Contain("t0.[Salary] BETWEEN t1.[MinSalary] AND (t1.[MinSalary] + @p0)"));
        Assert.That(sql.Parameters.Select(parameter => parameter.Value), Is.EqualTo(new object?[] { 10000 }));
    }

    [Test]
    public void QueryDefinitionJson_ParsesContextsAndJoinCondition()
    {
        const string json = """
            {
              "entity": "User",
              "projection": [{ "fields": ["UserContext.Id"] }],
              "contexts": [{
                "name": "UserContext",
                "joinType": "left",
                "query": {
                  "entity": "User",
                  "projection": [{ "fields": ["Id"] }]
                },
                "condition": {
                  "left": "ManagerId",
                  "operation": 0,
                  "right": "Id"
                }
              }]
            }
            """;
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new("Id", DataType.Number), new("ManagerId", DataType.Number)]));

        var sql = db.Query(QueryDefinitionParser.Parse(json)).ToSql();

        Assert.That(sql.CommandText, Does.Contain("LEFT JOIN [UserContext]"));
        Assert.That(sql.CommandText, Does.Contain("t0.[ManagerId] = t1.[Id]"));
    }

    [Test]
    public void QueryDefinitionWithContexts_CanStillBeComposed()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new("Id", DataType.Number), new("ManagerId", DataType.Number)]));
        var definition = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "UserContext",
                    new QueryDefinition { Entity = "User", Projection = [new("Id")] },
                    new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")))
            ],
            Projection = [new("UserContext.Id")]
        };

        var sql = db.Query(definition)
            .Where(record => record["Id"] > 0)
            .ToSql();

        Assert.That(sql.CommandText, Does.Contain("WHERE (t0.[Id] > 0)"));
        Assert.That(sql.Parameters, Is.Empty);
    }

    [Test]
    public void MonthlySalesTrend_RendersCteAggregatesAndWindowFunctions()
    {
        var monthlyQuery = new QueryDefinition
        {
            Entity = "Orders",
            Filter = new ConditionDefinition(
                LogicalOperator.And,
                new ConditionStatementDefinition(
                    new FieldDefinition("Status"),
                    BinaryOperator.Equals,
                    new ConstFieldDefinition("Completed"))
                {
                    Parameterized = true
                },
                new ConditionStatementDefinition(
                    new FieldDefinition("Items.Id"),
                    BinaryOperator.IsNotNull)),
            Projection =
            [
                Projection("SalesMonth", Function(
                    "MonthKey",
                    new FieldDefinition("OrderDate"))),
                Projection("Revenue", Function(
                    "SUM",
                    new GroupFieldDefintion(
                    [
                        new FieldDefinition("Items.Quantity"),
                        new FieldDefinition(MatchOperation.Multiply, "Items.UnitPrice"),
                        new FieldDefinition(MatchOperation.Subtract, "Items.DiscountAmount")
                    ]))),
                Projection("OrderCount", Function(
                    "CountDistinct",
                    new FieldDefinition("Id")))
            ]
        };
        var lagRevenue = Function(
            "Lag",
            new FieldDefinition("Revenue"),
            new ConstFieldDefinition(1),
            new FieldDefinition("SalesMonth"));
        var query = new QueryDefinition
        {
            Entity = "monthly",
            Contexts = [new QueryContextDefintion("monthly", monthlyQuery)],
            Projection =
            [
                new("SalesMonth"),
                Projection("Revenue", Function(
                    "ROUND",
                    new FieldDefinition("Revenue"),
                    new ConstFieldDefinition(2))),
                new("OrderCount"),
                Projection("PriorMonthRevenue", Function(
                    "ROUND",
                    lagRevenue,
                    new ConstFieldDefinition(2))),
                Projection("RevenueChange", Function(
                    "ROUND",
                    new GroupFieldDefintion(
                    [
                        new FieldDefinition("Revenue"),
                        new FunctionFieldDefinition(
                            MatchOperation.Subtract,
                            "Lag",
                            [
                                new FieldDefinition("Revenue"),
                                new ConstFieldDefinition(1),
                                new FieldDefinition("SalesMonth")
                            ])
                    ]),
                    new ConstFieldDefinition(2))),
                Projection("RunningRevenue", Function(
                    "ROUND",
                    Function(
                        "WindowSum",
                        new FieldDefinition("Revenue"),
                        new FieldDefinition("SalesMonth")),
                    new ConstFieldDefinition(2)))
            ],
            Sorts = [new(new FieldDefinition("SalesMonth"), QuerySortDirection.Descending)],
            Page = 1,
            PageSize = 10
        };

        var sql = _db.Query(query).ToSql();

        var data = _db.Query(query).ToList();
        Assert.That(data.Count, Is.GreaterThan(0));

        Assert.That(sql.CommandText, Does.Contain("WITH [monthly] AS"));
        Assert.That(sql.CommandText, Does.Contain("CONVERT"));
        Assert.That(sql.CommandText, Does.Match(@"SELECT\s+CONVERT\s*\(char\s*\(7\)"));
        Assert.That(sql.CommandText, Does.Not.Match(@"SELECT\s+\(CONVERT"));
        Assert.That(sql.CommandText, Does.Match(@"SUM\s*\("));
        Assert.That(sql.CommandText, Does.Match(@"COUNT\s*\(\s*DISTINCT"));
        Assert.That(sql.CommandText, Does.Contain("GROUP BY"));
        Assert.That(sql.CommandText, Does.Match(@"LAG\s*\("));
        Assert.That(sql.CommandText, Does.Match(@"ROWS BETWEEN UNBOUNDED PRECEDING\s+AND CURRENT ROW"));
        Assert.That(sql.CommandText, Does.Contain("ORDER BY"));
        Assert.That(sql.CommandText, Does.Contain("FETCH NEXT 10 ROWS ONLY"));
        Assert.That(sql.Parameters.Select(parameter => parameter.Value), Does.Contain("Completed"));
        Assert.That(sql.CommandText, Does.Contain("IS NOT NULL"));
    }

    private static DynamicSqlContext CreateContext(ISqlDialect dialect, params Entity[] entities)
        => new(new SqliteConnection(), new MetaProvider(entities), dialect);

    private static ProjectionDefinition Projection(string alias, IFieldDefinition expression)
        => new([expression], alias);

    private static FunctionFieldDefinition Function(string name, params IFieldDefinition[] arguments)
        => new(name, arguments);
}
