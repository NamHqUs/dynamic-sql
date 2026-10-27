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

    [TestCase(null)]
    [TestCase(0)]
    public void ContextDoesNotPageWhenPageSizeIsUnsetOrZero(int? pageSize)
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new("Id", DataType.Number), new("ManagerId", DataType.Number)]));
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "Manager",
                    new QueryDefinition
                    {
                        Entity = "User",
                        Projection = [new("Id")],
                        Page = 1,
                        PageSize = pageSize
                    },
                    new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")))
            ],
            Projection = [new("Manager.Id")]
        };

        var sql = db.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.Not.Contain("OFFSET"));
        Assert.That(sql, Does.Not.Contain("FETCH NEXT"));
        Assert.That(sql, Does.Not.Contain("ORDER BY ("));
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
    public void QueryDefinitionJson_UsesContextAliasesForRepeatedReferences()
    {
        const string json = """
            {
              "entity": "User",
              "projection": [
                { "fields": ["Manager.Id"] },
                { "fields": ["Reviewer.Id"] }
              ],
              "contexts": [
                {
                  "name": "UserContext",
                  "alias": "Manager",
                  "query": { "entity": "User", "projection": [{ "fields": ["Id"] }] },
                  "condition": { "left": "ManagerId", "operation": 0, "right": "Id" }
                },
                {
                  "name": "UserContext",
                  "alias": "Reviewer",
                  "query": { "entity": "User", "projection": [{ "fields": ["Id"] }] },
                  "condition": { "left": "ReviewerId", "operation": 0, "right": "Id" }
                }
              ]
            }
            """;
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [
                new("Id", DataType.Number),
                new("ManagerId", DataType.Number),
                new("ReviewerId", DataType.Number),
            ]));

        var sql = db.Query(QueryDefinitionParser.Parse(json)).ToSql().CommandText;

        Assert.That(sql, Does.Contain("WITH [Manager] AS"));
        Assert.That(sql, Does.Contain("[Reviewer] AS"));
        Assert.That(sql, Does.Contain("LEFT JOIN [Manager]"));
        Assert.That(sql, Does.Contain("LEFT JOIN [Reviewer]"));
        Assert.That(sql, Does.Contain("t1.[Id] [Manager.Id]"));
        Assert.That(sql, Does.Contain("t2.[Id] [Reviewer.Id]"));
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

    private static DynamicSqlContext CreateContext(ISqlDialect dialect, params Entity[] entities)
        => new(new SqliteConnection(), new MetaProvider(entities), dialect);

    private static ProjectionDefinition Projection(string alias, IFieldDefinition expression)
        => new([expression], alias);

    private static FunctionFieldDefinition Function(string name, params IFieldDefinition[] arguments)
        => new(name, arguments);
}
