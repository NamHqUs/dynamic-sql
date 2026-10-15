using Microsoft.Data.Sqlite;
using System.Globalization;

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
        var subtract = new FunctionDefinition(
            name: "SUBTRACT",
            arguments:
            [
                FunctionArgumentDefinition.QueryExpression("left"),
                FunctionArgumentDefinition.QueryExpression("right")
            ],
            resultType: typeof(decimal),
            description: "Subtracts one expression from another.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = "SUB({1}, {2})",
                ["Sqlite"] = "({1} - {2})",
                ["Postgres"] = "({2} - {1})"
            });
        var call = new FunctionFieldDto("SUBTRACT", [new FieldDto("Amount"), new ConstFieldDto(2)]);

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
    public void FunctionTemplateCanFixSqlTypeAndAcceptNestedExpression()
    {
        var today = new FunctionDefinition(
            name: "TODAY",
            arguments: [FunctionArgumentDefinition.QueryExpression("value")],
            resultType: typeof(DateTime),
            description: "Returns the date portion of a value.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = "CAST({1} AS DATE)",
                ["Sqlite"] = "CAST({1} AS DATE)",
                ["Postgres"] = "CAST({1} AS DATE)"
            });
        var expression = new FunctionFieldDto("TODAY",
        [
            new GroupFieldDto(
            [
                new FieldDto("Amount"),
                new BinaryFieldDto("+", new ConstFieldDto(3))
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
        var add = new FunctionDefinition(
            name: "ADD",
            arguments:
            [
                FunctionArgumentDefinition.QueryExpression("left"),
                FunctionArgumentDefinition.QueryExpression(
                    "right",
                    optional: true,
                    defaultValue: FunctionArgumentDefault.Constant(12))
            ],
            resultType: typeof(decimal),
            description: "Adds two numeric expressions.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = "({1} + {2})",
                ["Sqlite"] = "({1} + {2})",
                ["Postgres"] = "({1} + {2})"
            });

        var sql = GenerateSql(
            new FunctionFieldDto("ADD", [new FieldDto("Amount")]),
            [add],
            new SqliteDialect());

        Assert.That(sql.CommandText, Does.Contain("(t0.\"Amount\" + @p0)"));
        Assert.That(sql.Parameters.Single().Value, Is.EqualTo(12m));
    }

    [Test]
    public void CountSupportsOmittedWildcardExplicitWildcardAndFieldArguments()
    {
        var countWithoutArgument = GenerateSql(
            new FunctionFieldDto("COUNT", []),
            dialect: new SqliteDialect());
        var countWildcard = GenerateSql(
            new FunctionFieldDto("COUNT", [new FieldDto("*")]),
            dialect: new SqliteDialect());
        var countField = GenerateSql(
            new FunctionFieldDto("COUNT", [new FieldDto("Id")]),
            dialect: new SqliteDialect());

        Assert.That(countWithoutArgument.CommandText, Does.Match(@"COUNT\s*\(\s*\*\s*\)"));
        Assert.That(countWildcard.CommandText, Does.Match(@"COUNT\s*\(\s*\*\s*\)"));
        Assert.That(countField.CommandText, Does.Match(@"COUNT\s*\(\s*t0\.""Id""\s*\)"));
    }

    [Test]
    public void LogicalTypeArgumentUsesMetadataMappingForCurrentDialect()
    {
        var cast = new FunctionDefinition(
            name: "CAST_TO_MONEY",
            arguments:
            [
                FunctionArgumentDefinition.QueryExpression("value"),
                FunctionArgumentDefinition.SqlType("type", ["Money"])
            ],
            resultType: typeof(decimal),
            description: "Converts a value to the selected logical SQL type.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = "CAST({1} AS {2})",
                ["Sqlite"] = "CAST({1} AS {2})",
                ["Postgres"] = "CAST({1} AS {2})"
            });
        var moneyType = new SqlTypeDefinition("Money", new Dictionary<string, string>
        {
            ["SqlServer"] = "decimal(19,4)",
            ["Sqlite"] = "NUMERIC",
            ["Postgres"] = "numeric(19,4)"
        });

        var sql = GenerateSql(
            new FunctionFieldDto("CAST_TO_MONEY", [new FieldDto("Amount"), new ConstFieldDto("Money")]),
            [cast],
            new SqliteDialect(),
            [moneyType]);

        Assert.That(sql.CommandText, Does.Match(
            @"CAST\s*\(\s*t0\.""Amount""\s+AS\s+NUMERIC\s*\)"));
        Assert.That(sql.Parameters, Is.Empty);
    }

    [Test]
    public void MetaProviderExposesBuiltInAndCustomFunctionMetadata()
    {
        var customFunction = new FunctionDefinition(
            name: "TODAY",
            arguments: [FunctionArgumentDefinition.QueryExpression("value")],
            resultType: typeof(DateTime),
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = "CAST({1} AS DATE)",
                ["Sqlite"] = "CAST({1} AS DATE)",
                ["Postgres"] = "CAST({1} AS DATE)"
            },
            description: "Returns the date portion of a value.");
        var customType = new SqlTypeDefinition("Currency", new Dictionary<string, string>
        {
            ["SqlServer"] = "money",
            ["Sqlite"] = "NUMERIC",
            ["Postgres"] = "money"
        });
        var metadata = new MetaProvider(Metadata, [customFunction], [customType]);

        Assert.That(metadata.GetFunctions().Select(function => function.Name),
            Does.Contain("TODAY").And.Contain("SUM"));
        Assert.That(metadata.GetFunction("today"), Is.SameAs(customFunction));
        Assert.That(customFunction.Description, Is.EqualTo("Returns the date portion of a value."));
        Assert.That(metadata.GetSqlTypes().Select(type => type.Name), Does.Contain("Currency"));
    }

    [Test]
    public void BuiltInDateFunctionsHaveFriendlyNamesDescriptionsAndProviderSql()
    {
        var functions = new MetaProvider(Metadata).GetFunctions().ToDictionary(
            function => function.Name,
            StringComparer.OrdinalIgnoreCase);

        Assert.That(functions.Keys, Does.Contain("DateSub").And.Contain("MonthSub").And.Contain("YearSub")
            .And.Contain("DateAdd").And.Contain("MonthAdd").And.Contain("YearAdd").And.Contain("DateFormat"));
        Assert.That(functions.Keys, Does.Not.Contain("DATEDIFF").And.Not.Contain("STRFTIME"));
        Assert.That(functions.Values.All(function => !string.IsNullOrWhiteSpace(function.Description)), Is.True);

        var dateAdd = new FunctionFieldDto("DateAdd",
            [new FieldDto("InvoiceDate"), new ConstFieldDto(2)]);
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
            new FunctionFieldDto("DateFormat",
                [new FieldDto("InvoiceDate"), new ConstFieldDto("%Y-%m")]),
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
            new MetaProvider(Metadata),
            new SqliteDialect());

        var cases = new Dictionary<string, DateTime>
        {
            ["DateAdd"] = new(2025, 1, 16, 10, 30, 0),
            ["MonthAdd"] = new(2025, 2, 15, 10, 30, 0),
            ["YearAdd"] = new(2026, 1, 15, 10, 30, 0)
        };
        foreach (var (functionName, expected) in cases)
        {
            var record = context.Query(new QueryDto
            {
                Entity = "Items",
                Fields =
                [
                    new ProjectionDto(
                        new FunctionFieldDto(functionName,
                            [new FieldDto("InvoiceDate"), new ConstFieldDto(1)]),
                        "Result")
                ]
            }).ToList().Single();

            Assert.That(Convert.ToDateTime(record["Result"], CultureInfo.InvariantCulture), Is.EqualTo(expected));
        }
    }

    [Test]
    public void MissingDialectTemplateFailsExplicitly()
    {
        var custom = new FunctionDefinition(
            name: "SQLSERVER_ONLY",
            arguments: [FunctionArgumentDefinition.QueryExpression("value")],
            resultType: typeof(decimal),
            description: "A SQL Server-only test function.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["SqlServer"] = "CUSTOM({1})"
            });

        Assert.Throws<FormatException>(() =>
            GenerateSql(
                new FunctionFieldDto("SQLSERVER_ONLY", [new FieldDto("Amount")]),
                [custom],
                new SqliteDialect()));
    }

    [Test]
    public void NewDialectUsesGenericBuiltInsAndAcceptsPluginSpecificTemplates()
    {
        var toDate = new FunctionDefinition(
            name: "TO_DATE",
            arguments: [FunctionArgumentDefinition.QueryExpression("value")],
            resultType: typeof(DateTime),
            description: "Parses a date value.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["Oracle"] = "TO_DATE({1}, 'YYYY-MM-DD')"
            });
        var oracle = new OraclePluginDialect();

        var customFunctionSql = GenerateSql(
            new FunctionFieldDto("TO_DATE", [new FieldDto("Amount")]),
            [toDate],
            oracle);
        var builtInSumSql = GenerateSql(
            new FunctionFieldDto("SUM", [new FieldDto("Amount")]),
            dialect: oracle);

        Assert.That(customFunctionSql.CommandText, Does.Match(@"TO_DATE\s*\("));
        Assert.That(builtInSumSql.CommandText, Does.Match(@"SUM\s*\("));
    }

    [Test]
    public void InvalidFunctionArgumentsAndTemplatePlaceholdersAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new FunctionDefinition(
                name: "INVALID",
                arguments: [FunctionArgumentDefinition.QueryExpression("value")],
                resultType: typeof(decimal),
                description: "An invalid template test function.",
                sqlTemplates: new Dictionary<string, string>
                {
                    ["Sqlite"] = "INVALID({2})"
                }));

        var twice = new FunctionDefinition(
            name: "TWICE",
            arguments: [FunctionArgumentDefinition.QueryExpression("value")],
            resultType: typeof(decimal),
            description: "Doubles a numeric value.",
            sqlTemplates: new Dictionary<string, string>
            {
                ["Sqlite"] = "({1} * 2)"
            });

        Assert.Throws<FormatException>(() =>
            GenerateSql(new FunctionFieldDto("TWICE", []), [twice], new SqliteDialect()));
    }

    private static SqlQuery GenerateSql(
        IFieldDto expression,
        IEnumerable<FunctionDefinition>? functions = null,
        ISqlDialect? dialect = null,
        IEnumerable<SqlTypeDefinition>? sqlTypes = null)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        var metadata = new MetaProvider(Metadata, functions, sqlTypes);
        using var context = new DynamicSqlContext(connection, metadata, dialect ?? new SqliteDialect());

        return context.Query(new QueryDto
        {
            Entity = "Items",
            Fields = [new ProjectionDto(expression, "Result")]
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
