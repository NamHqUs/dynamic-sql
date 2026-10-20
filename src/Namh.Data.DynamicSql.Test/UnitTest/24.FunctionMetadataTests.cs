using Demo.Share;
using Microsoft.Data.Sqlite;
using System.Globalization;
using static Namh.Data.DynamicSql.SqlFunctionDefintion;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public sealed class _24_FunctionMetadataTests
{
    private static readonly Entity[] Metadata =
    [
        new("Items",
        [
            new("Id", DataType.Number),
            new("Amount", DataType.Number),
            new("InvoiceDate", DataType.Datetime)
        ])
    ];

    [Test]
    public void UserDefinedTemplateUsesActiveDialectAndKeepsExpressionParameters()
    {
        var subtract = new SqlFunctionDefintion(
            name: "SUBTRACT",
            arguments:
            [
                new("left", DataType.Number),
                new("right", DataType.Number)
            ],
            resultType: DataType.Number,
            description: "Subtracts one expression from another.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = "SUB({1}, {2})",
                ["Sqlite"] = "({left} - {2})",
                ["Postgres"] = "({2} - {1})"
            });
        var call = new FunctionFieldDefinition("SUBTRACT", [new FieldDefinition("Amount"), new ConstFieldDefinition(2)]);

        var sqlServerSql = GenerateSql(call, [subtract], new SqlServerDialect());
        var sqliteSql = GenerateSql(call, [subtract], new SqliteDialect());
        var postgresSql = GenerateSql(call, [subtract], new PostgresDialect());

        Assert.That(sqlServerSql.CommandText, Does.Match(@"SUB\s*\(\s*t0\.\[Amount\],\s*@p0\s*\)"));
        Assert.That(sqliteSql.CommandText, Does.Match(@"\(\s*t0\.""Amount""\s*-\s*@p0\s*\)"));
        Assert.That(postgresSql.CommandText, Does.Match(@"\(\s*@p0\s*-\s*t0\.""Amount""\s*\)"));
        Assert.That(sqlServerSql.Parameters.Single().Value, Is.EqualTo(2m));
        Assert.That(sqliteSql.Parameters.Single().Value, Is.EqualTo(2m));
        Assert.That(postgresSql.Parameters.Single().Value, Is.EqualTo(2m));
    }

    [Test]
    public void FunctionTemplateCanCastNestedExpression()
    {
        var today = new SqlFunctionDefintion(
            name: "TODAY",
            arguments: [new("value", DataType.Datetime)],
            resultType: DataType.Datetime,
            description: "Returns the date portion of a value.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = "CAST({1} AS DATE)",
                ["Sqlite"] = "CAST({1} AS DATE)",
                ["Postgres"] = "CAST({1} AS DATE)"
            });
        var expression = new FunctionFieldDefinition("TODAY",
        [
            new GroupFieldDefintion(
            [
                new FieldDefinition("Amount"),
                new BinaryFieldDefinition("+", new ConstFieldDefinition(3))
            ])
        ]);

        var sql = GenerateSql(expression, [today], new SqliteDialect());

        Assert.That(sql.CommandText, Does.Match(
            @"CAST\s*\(\s*\(t0\.""Amount""\s*\+\s*@p0\)\s+AS\s+DATE\s*\)"));
        Assert.That(sql.Parameters.Single().Value, Is.EqualTo(3m));
    }

    [Test]
    public void OptionalExpressionDefaultIsParameterized()
    {
        var add = new SqlFunctionDefintion(
            name: "ADD",
            arguments:
            [
                new("left", DataType.Number),
                new("right", DataType.Number, 12m)
            ],
            resultType: DataType.Number,
            description: "Adds two numeric expressions.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = "({1} + {2})",
                ["Sqlite"] = "({1} + {2})",
                ["Postgres"] = "({1} + {2})"
            });

        var sql = GenerateSql(
            new FunctionFieldDefinition("ADD", [new FieldDefinition("Amount")]),
            [add],
            new SqliteDialect());

        Assert.That(sql.CommandText, Does.Contain("(t0.\"Amount\" + @p0)"));
        Assert.That(sql.Parameters.Single().Value, Is.EqualTo(12m));
    }

    [Test]
    public void CountSupportsOmittedWildcardExplicitWildcardAndFieldArguments()
    {
        var countWithoutArgument = GenerateSql(
            new FunctionFieldDefinition("COUNT", []),
            dialect: new SqliteDialect());
        var countWildcard = GenerateSql(
            new FunctionFieldDefinition("COUNT", [new FieldDefinition("*")]),
            dialect: new SqliteDialect());
        var countField = GenerateSql(
            new FunctionFieldDefinition("COUNT", [new FieldDefinition("Id")]),
            dialect: new SqliteDialect());

        Assert.That(countWithoutArgument.CommandText, Does.Match(@"COUNT\s*\(\s*\*\s*\)"));
        Assert.That(countWildcard.CommandText, Does.Match(@"COUNT\s*\(\s*\*\s*\)"));
        Assert.That(countField.CommandText, Does.Match(@"COUNT\s*\(\s*t0\.""Id""\s*\)"));
    }

    [Test]
    public void ArgumentDefaultUsesItsDeclaredDataType()
    {
        var add = new SqlFunctionDefintion(
            name: "ADD_DEFAULT",
            arguments:
            [
                new("value", DataType.Number),
                new("amount", DataType.Number, 4m)
            ],
            resultType: DataType.Number,
            description: "Adds a typed default amount.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = "({value} + {amount})",
                ["Sqlite"] = "({value} + {amount})",
                ["Postgres"] = "({value} + {amount})"
            });

        var sql = GenerateSql(
            new FunctionFieldDefinition("ADD_DEFAULT", [new FieldDefinition("Amount")]),
            [add],
            new SqliteDialect());

        Assert.That(sql.CommandText, Does.Contain("(t0.\"Amount\" + @p0)"));
        Assert.That(sql.Parameters.Single().Value, Is.EqualTo(4m));
    }

    [Test]
    public void ArgumentDefaultMustMatchItsDeclaredDataType()
    {
        Assert.Throws<ArgumentException>(() =>
            new SqlFunctionArgumentDefinition("enabled", DataType.Boolean, "true"));
        Assert.That(new SqlFunctionArgumentDefinition("value", DataType.Number, "*").Optional, Is.True);
        Assert.That(new SqlFunctionArgumentDefinition("value", DataType.Number).Optional, Is.False);
    }

    [Test]
    public void FunctionResultUsesLogicalDataType()
    {
        var numericFunction = new SqlFunctionDefintion(
            "NUMERIC_RESULT",
            [],
            DataType.Number,
            "Returns a numeric value.",
            sqlTemplates: new Dictionary<string, string> { ["Sqlite"] = "1" });

        Assert.That(numericFunction.ResultType, Is.EqualTo(DataType.Number));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SqlFunctionDefintion(
                "INVALID_RESULT",
                [],
                (DataType)int.MaxValue,
                "Has an unsupported result type.",
                sqlTemplates: new Dictionary<string, string> { ["Sqlite"] = "1" }));
    }

    [Test]
    public void MetaProviderExposesBuiltInAndCustomFunctionMetadata()
    {
        var customFunction = new SqlFunctionDefintion(
            name: "TODAY",
            arguments: [new("value", DataType.Datetime)],
            resultType: DataType.Datetime,
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = "CAST({1} AS DATE)",
                ["Sqlite"] = "CAST({1} AS DATE)",
                ["Postgres"] = "CAST({1} AS DATE)"
            },
            description: "Returns the date portion of a value.");
        var metadata = new MetaProvider(Metadata, [customFunction]);

        Assert.That(metadata.GetFunctions().Select(function => function.Name),
            Does.Contain("TODAY").And.Contain("SUM"));
        Assert.That(metadata.GetFunction("today"), Is.SameAs(customFunction));
        Assert.That(customFunction.Description, Is.EqualTo("Returns the date portion of a value."));
    }

    [Test]
    public void BuiltInDateFunctionsHaveFriendlyNamesDescriptionsAndProviderSql()
    {
        var functions = new MetaProvider(Metadata, CustomFunctionDefintions.Functions).GetFunctions().ToDictionary(
            function => function.Name,
            StringComparer.OrdinalIgnoreCase);

        Assert.That(functions.Keys, Does.Contain("DateSub").And.Contain("MonthSub").And.Contain("YearSub")
            .And.Contain("DateAdd").And.Contain("MonthAdd").And.Contain("YearAdd").And.Contain("DateFormat"));
        Assert.That(functions.Keys, Does.Not.Contain("DATEDIFF").And.Not.Contain("STRFTIME"));
        Assert.That(functions.Values.All(function => !string.IsNullOrWhiteSpace(function.Description)), Is.True);
        Assert.That(functions["COUNT"].ResultType, Is.EqualTo(DataType.Number));
        Assert.That(functions["DateAdd"].ResultType, Is.EqualTo(DataType.Datetime));

        var dateAdd = new FunctionFieldDefinition("DateAdd",
            [new FieldDefinition("InvoiceDate"), new ConstFieldDefinition(2)]);
        var sqlServerSql = GenerateSql(dateAdd, dialect: new SqlServerDialect());
        var sqliteSql = GenerateSql(dateAdd, dialect: new SqliteDialect());
        var postgresSql = GenerateSql(dateAdd, dialect: new PostgresDialect());

        Assert.That(sqlServerSql.CommandText,
            Does.Match(@"DATEADD\s*\(\s*day,\s*@p0,\s*t0\.\[InvoiceDate\]\s*\)"));
        Assert.That(sqliteSql.CommandText,
            Does.Match(@"datetime\s*\(\s*t0\.""InvoiceDate"",\s*printf\s*\(\s*'%\+d days',\s*@p0\s*\)\s*\)"));
        Assert.That(postgresSql.CommandText,
            Does.Match(@"t0\.""InvoiceDate""\s*\+\s*\(\s*@p0\s*\*\s*INTERVAL '1 day'\s*\)"));

        var dateFormat = GenerateSql(
            new FunctionFieldDefinition("DateFormat",
                [new FieldDefinition("InvoiceDate"), new ConstFieldDefinition("%Y-%m")]),
            dialect: new SqliteDialect());
        Assert.That(dateFormat.CommandText,
            Does.Match(@"STRFTIME\s*\(\s*@p0,\s*t0\.""InvoiceDate""\s*\)"));
        Assert.That(dateFormat.Parameters.Single().Value, Is.EqualTo("%Y-%m"));
    }

    [Test]
    public void BuiltInDateAddFunctionsAdjustDatesOnSqlite()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE "Items" ("InvoiceDate" TEXT NOT NULL);
                INSERT INTO "Items" ("InvoiceDate") VALUES ('2025-01-15 10:30:00');
                """;
            command.ExecuteNonQuery();
        }

        using var context = new DynamicSqlContext(
            connection,
            new MetaProvider(Metadata, CustomFunctionDefintions.Functions),
            new SqliteDialect());

        var cases = new Dictionary<string, DateTime>
        {
            ["DateAdd"] = new(2025, 1, 16, 10, 30, 0),
            ["MonthAdd"] = new(2025, 2, 15, 10, 30, 0),
            ["YearAdd"] = new(2026, 1, 15, 10, 30, 0)
        };
        foreach (var (functionName, expected) in cases)
        {
            var record = context.Query(new QueryDefinition
            {
                Entity = "Items",
                Projection =
                [
                    new ProjectionDefinition(
                        new FunctionFieldDefinition(functionName,
                            [new FieldDefinition("InvoiceDate"), new ConstFieldDefinition(1)]),
                        "Result")
                ]
            }).ToList().Single();

            Assert.That(Convert.ToDateTime(record["Result"], CultureInfo.InvariantCulture), Is.EqualTo(expected));
        }
    }

    [Test]
    public void MissingDialectTemplateFailsExplicitly()
    {
        var custom = new SqlFunctionDefintion(
            name: "SQLSERVER_ONLY",
            arguments: [new("value", DataType.Number)],
            resultType: DataType.Number,
            description: "A SQL Server-only test function.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = "CUSTOM({1})"
            });

        Assert.Throws<FormatException>(() =>
            GenerateSql(
                new FunctionFieldDefinition("SQLSERVER_ONLY", [new FieldDefinition("Amount")]),
                [custom],
                new SqliteDialect()));
    }

    [Test]
    public void NewDialectRequiresPluginSpecificTemplates()
    {
        var toDate = new SqlFunctionDefintion(
            name: "TO_DATE",
            arguments: [new("value", DataType.Number)],
            resultType: DataType.Datetime,
            description: "Parses a date value.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["Oracle"] = "TO_DATE({1}, 'YYYY-MM-DD')"
            });
        var oracle = new OraclePluginDialect();

        var customFunctionSql = GenerateSql(
            new FunctionFieldDefinition("TO_DATE", [new FieldDefinition("Amount")]),
            [toDate],
            oracle);
        Assert.That(customFunctionSql.CommandText, Does.Match(@"TO_DATE\s*\("));
        Assert.Throws<FormatException>(() =>
            GenerateSql(
                new FunctionFieldDefinition("SUM", [new FieldDefinition("Amount")]),
                dialect: oracle));
    }

    [Test]
    public void InvalidFunctionArgumentsAndTemplatePlaceholdersAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new SqlFunctionDefintion(
                name: "INVALID",
                arguments: [new("value", DataType.Number)],
                resultType: DataType.Number,
                description: "An invalid template test function.",
                sqlTemplates: new Dictionary<string, string>
                {
                    ["Sqlite"] = "INVALID({2})"
                }));

        var twice = new SqlFunctionDefintion(
            name: "TWICE",
            arguments: [new("value", DataType.Number)],
            resultType: DataType.Number,
            description: "Doubles a numeric value.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["Sqlite"] = "({1} * 2)"
            });

        Assert.Throws<FormatException>(() =>
            GenerateSql(new FunctionFieldDefinition("TWICE", []), [twice], new SqliteDialect()));
    }

    private static SqlQuery GenerateSql(
        IFieldDefinition expression,
        IEnumerable<SqlFunctionDefintion>? functions = null,
        ISqlDialect? dialect = null)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        var metadata = new MetaProvider(Metadata, functions?? CustomFunctionDefintions.Functions);
        using var context = new DynamicSqlContext(connection, metadata, dialect ?? new SqliteDialect());

        return context.Query(new QueryDefinition
        {
            Entity = "Items",
            Projection = [new ProjectionDefinition(expression, "Result")]
        }).ToSql();
    }

    private sealed class OraclePluginDialect : ISqlDialect
    {
        public string Key => "Oracle";

        public string QuoteIdentifier(string identifier)
            => $"\"{identifier.Replace("\"", "\"\"")}\"";

        public string RenderTable(Entity entity)
            => $"{QuoteIdentifier(entity.Name)}";

        public string RenderBoolean(bool value) => value ? "1" : "0";

        public string RenderStringLiteral(string value)
            => $"'{value.Replace("'", "''")}'";

        public string RenderStringConcatenation(string left, string right)
            => $"{left} || {right}";

        public string RenderTakePrefix(int count) => string.Empty;

        public string RenderPaging(int? skip, int? take) => string.Empty;

        public string FormatParameterValue(object? value) => value?.ToString() ?? "NULL";
    }
}
