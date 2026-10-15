namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public class _14_QueryRequestTests : TestBase
{
    [Test]
    public void DefinitionBuildsParameterizedSql()
    {
        var definition = new QueryDto
        {
            Entity = "User",
            Fields =
            [
                ProjectionDto.FromText("Name"),
                ProjectionDto.FromText("Manager.Name | ManagerName")
            ],
            Filter = new FilterDto(
                LogicalOperator.And,
                new FilterItemDto(
                    LogicalOperator.And,
                    new FieldDto("Age"),
                    BinaryOperator.GreaterThan,
                    new ConstFieldDto(18))),
            Sorts =
            [
                new SortDto(new FieldDto("Name"))
            ],
            Page = 1,
            PageSize = 10
        };

        var sql = _db.Query(definition).ToSql();

        Assert.That(sql.CommandText, Does.Contain("SELECT"));
        Assert.That(sql.CommandText, Does.Contain("ORDER BY"));
        Assert.That(sql.CommandText, Does.Contain("FETCH NEXT 10 ROWS ONLY"));
        Assert.That(sql.CommandText, Does.Contain("Age"));
    }

    [Test]
    public void AggregateFilterUsesHavingInsteadOfScalarSubquery()
    {
        var definition = new QueryDto
        {
            Entity = "User",
            Fields =
            [
                ProjectionDto.FromText("Name"),
                ProjectionDto.FromText("Average(Age)")
            ],
            Filter = new FilterDto(
                LogicalOperator.And,
                new FilterItemDto(
                    LogicalOperator.And,
                    new FieldDto("Average(Age)"),
                    BinaryOperator.GreaterThan,
                    new ConstFieldDto(12))),
            PageSize = 50
        };

        var sql = _db.Query(definition).ToSql().CommandText;

        Assert.That(sql, Does.Contain("GROUP BY t0.[Name]"));
        Assert.That(sql, Does.Contain("HAVING (AVG (t0.[Age]) >"));
        Assert.That(sql, Does.Not.Contain("WHERE (("));
        Assert.That(sql, Does.Not.Contain("SELECT AVG"));
    }

    [Test]
    public void DefinitionExecutesWithProjectionAndFilter()
    {
        var definition = new QueryDto
        {
            Entity = "User",
            Fields = [ProjectionDto.FromText("Name")],
            Filter = new FilterDto(
                LogicalOperator.And,
                new FilterItemDto(
                    LogicalOperator.And,
                    new FieldDto("Name"),
                    BinaryOperator.Equals,
                    new ConstFieldDto("Alice")))
        };

        var records = _db.Query(definition).ToList();

        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0]["Name"], Is.EqualTo("Alice"));
    }

    [Test]
    public void DefinitionExecutesFilterWithFieldReferenceOnRight()
    {
        var definition = new QueryDto
        {
            Entity = "User",
            Filter = new FilterDto(
                LogicalOperator.And,
                new FilterItemDto(
                    LogicalOperator.And,
                    new FieldDto("Age"),
                    BinaryOperator.GreaterThan,
                    new FieldDto("Id")))
        };

        var query = _db.Query(definition);
        var sql = query.ToSql();
        var records = query.ToList();

        Assert.That(sql.CommandText, Does.Contain("Age"));
        Assert.That(sql.CommandText, Does.Contain("Id"));
        Assert.That(records, Is.Not.Null);
    }

    [Test]
    public void DefinitionSupportsAggregateOnRootEntity()
    {
        var definition = new QueryDto
        {
            Entity = "User",
            Fields =
            [
                ProjectionDto.FromText("Name"),
                ProjectionDto.FromText("Count() | InvoiceCount"),
                ProjectionDto.FromText("Sum(Age) | Revenue"),
                ProjectionDto.FromText("Average(Age) | AverageOrderValue")
            ]
        };

        var sql = _db.Query(definition).ToSql();
        var records = _db.Query(definition).ToList();

        Assert.That(sql.CommandText, Does.Contain("GROUP BY"));
        Assert.That(sql.CommandText, Does.Match(@"(?i)\bCOUNT\s*\(\s*\*\s*\)"));
        Assert.That(sql.CommandText, Does.Match(@"(?i)\bSUM\s*\("));
        Assert.That(sql.CommandText, Does.Match(@"(?i)\bAVG\s*\("));
        Assert.That(sql.CommandText, Does.Not.Match(@"(?i)\bSELECT\s+COUNT\s*\(\s*\*\s*\)"));
        Assert.That(sql.CommandText, Does.Not.Contain("Average("));
        Assert.That(records, Is.Not.Empty);
        Assert.That(records[0]["InvoiceCount"], Is.EqualTo(1L));
    }

    [Test]
    public void DefinitionSupportsBracketAggregateFunctions()
    {
        var definition = new QueryDto
        {
            Entity = "User",
            Fields =
            [
                ProjectionDto.FromText("Name"),
                ProjectionDto.FromText("Count[Id] | InvoiceCount"),
                ProjectionDto.FromText("Sum[Age] | TotalAge"),
                ProjectionDto.FromText("Average[Age] | AverageAge")
            ]
        };

        var sql = _db.Query(definition).ToSql().CommandText;

        Assert.That(sql, Does.Contain("GROUP BY"));
        Assert.That(sql, Does.Match(@"(?i)\bCOUNT\s*\("));
        Assert.That(sql, Does.Match(@"(?i)\bSUM\s*\("));
        Assert.That(sql, Does.Match(@"(?i)\bAVG\s*\("));
    }

    [Test]
    public void DefinitionSupportsWildcardAndEmptyCountArguments()
    {
        var definition = new QueryDto
        {
            Entity = "User",
            Fields =
            [
                ProjectionDto.FromText("Name"),
                ProjectionDto.FromText("Count[*] | WildcardCount"),
                ProjectionDto.FromText("Count[] | EmptyCount")
            ]
        };

        var query = _db.Query(definition);
        var sql = query.ToSql().CommandText;
        var records = query.ToList();

        Assert.That(sql, Does.Match(@"(?i)\bCOUNT\s*\(\s*\*\s*\)"));
        Assert.That(records, Is.Not.Empty);
        Assert.That(records.All(record => Convert.ToInt64(record["WildcardCount"]) == 1), Is.True);
        Assert.That(records.All(record => Convert.ToInt64(record["EmptyCount"]) == 1), Is.True);
    }

    [Test]
    public void DefinitionSortsByCollectionAggregate()
    {
        var definition = new QueryDto
        {
            Entity = "User",
            Fields =
            [
                ProjectionDto.FromText("Name"),
                ProjectionDto.FromText("Sum(Members.Age)")
            ],
            Sorts = [new SortDto(new FunctionFieldDto("Sum", [new FieldDto("Members.Age")]))]
        };

        var sql = _db.Query(definition).ToSql();
        var records = _db.Query(definition).ToList();

        Assert.That(sql.CommandText, Does.Contain("ORDER BY SUM ("));
        Assert.That(sql.CommandText, Does.Not.Contain("ORDER BY (\n    SELECT"));
        Assert.That(records.Select(record => record["Name"]),
            Is.EqualTo(new object?[] { "Charlie", "Eve", "Frank", "Bob", "David", "Alice" }));
    }

    [Test]
    public void JsonDefinitionSupportsStructuredSortExpressions()
    {
        const string json = """
            {
              "entity": "User",
              "sorts": [{
                "field": {
                  "kind": "function",
                  "function": "Sum",
                  "arguments": [{ "kind": "field", "field": "Age" }]
                },
                "direction": 1
              }]
            }
            """;

        var definition = QueryDto.FromJson(json);
        var sort = definition.Sorts.Single();
        var sql = _db.Query(definition).ToSql().CommandText;
        var serialized = System.Text.Json.JsonSerializer.Serialize(
            definition,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var roundTrippedSort = QueryDto.FromJson(serialized).Sorts.Single();

        Assert.That(sort.Field, Is.TypeOf<FunctionFieldDto>());
        Assert.That(sort.Direction, Is.EqualTo(QuerySortDirection.Descending));
        Assert.That(roundTrippedSort.Field, Is.TypeOf<FunctionFieldDto>());
        Assert.That(roundTrippedSort.Direction, Is.EqualTo(QuerySortDirection.Descending));
        Assert.That(sql, Does.Contain("ORDER BY SUM ("));
        Assert.That(sql, Does.Contain("DESC"));
    }

    [Test]
    public void DefinitionConnectsStructuredFiltersWithLogicalOperators()
    {
        var definition = new QueryDto
        {
            Entity = "User",
            Filter = new FilterDto(
                LogicalOperator.And,
                new FilterDto(
                    LogicalOperator.Or,
                    new FilterItemDto(
                        LogicalOperator.Or,
                        new FieldDto("Name"),
                        BinaryOperator.Equals,
                        new ConstFieldDto("Alice")),
                    new FilterItemDto(
                        LogicalOperator.Or,
                        new FieldDto("Age"),
                        BinaryOperator.GreaterThan,
                        new ConstFieldDto(18))),
                new FilterItemDto(
                    LogicalOperator.And,
                    new FieldDto("Name"),
                    BinaryOperator.Equals,
                    new ConstFieldDto("Bob")))
        };

        var sql = _db.Query(definition).ToSql();

        Assert.That(sql.CommandText, Does.Contain("OR"));
        Assert.That(sql.CommandText, Does.Contain("AND"));
        Assert.That(sql.CommandText, Does.Not.Contain("= 12"));
    }

    [Test]
    public void DefinitionRejectsInvalidPaging()
    {
        var definition = new QueryDto
        {
            Entity = "User",
            Page = 0,
            PageSize = 10
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => _db.Query(definition));
    }

    [Test]
    public void DefinitionFormatsAsReadableText()
    {
        var definition = new QueryDto
        {
            Entity = "User",
            Fields = [ProjectionDto.FromText("Id"), ProjectionDto.FromText("Name")],
            Filter = new FilterDto(
                LogicalOperator.And,
                new FilterDto(
                    LogicalOperator.And,
                    new FilterItemDto(
                        LogicalOperator.And,
                        new FieldDto("Id"),
                        BinaryOperator.GreaterThan,
                        new ConstFieldDto(3)),
                    new FilterItemDto(
                        LogicalOperator.And,
                        new FieldDto("Invoices.Id"),
                        BinaryOperator.LessThan,
                        new ConstFieldDto(3))),
                new FilterDto(
                    LogicalOperator.Or,
                    new FilterItemDto(
                        LogicalOperator.Or,
                        new FieldDto("Name"),
                        BinaryOperator.Equals,
                        new ConstFieldDto("abc")),
                    new FilterItemDto(
                        LogicalOperator.Or,
                        new FieldDto("Age"),
                        BinaryOperator.GreaterThan,
                        new ConstFieldDto(18)))),
            Sorts = [new SortDto(new FieldDto("Name"))],
            Page = 1,
            PageSize = 25,
            IncludeArchive = true
        };

        var expected = string.Join(Environment.NewLine,
            "Entity: User",
            "Fields: Id, Name",
            "Filters: (Id > <3> AND Invoices.Id < <3>) AND (Name = <abc> OR Age > <18>)",
            "Sorts: Name ASC",
            "Page: 1",
            "PageSize: 25",
            "IncludeArchive: True");

        Assert.That(definition.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void DefinitionFormatsEmptyValuesAsBlank()
    {
        var definition = new QueryDto { Entity = "User" };

        var expected = string.Join(Environment.NewLine,
            "Entity: User",
            "Fields: ",
            "Filters: ",
            "Sorts: ",
            "Page: ",
            "PageSize: ",
            "IncludeArchive: False");

        Assert.That(definition.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void DefinitionJsonDeserializesRecursiveFilterValues()
    {
        const string json = """
            {
              "entity": "User",
              "filter": {
                "logicalOperation": 0,
                "filters": [{
                  "logicalOperator": 0,
                  "field": { "kind": "field", "field": "Name" },
                  "operation": 0,
                  "value": "Alice",
                  "parameterized": false
                }]
              }
            }
            """;

        var definition = System.Text.Json.JsonSerializer.Deserialize<QueryDto>(
            json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.That(definition.Filter, Is.TypeOf<FilterDto>());
        var filter = definition.Filter!.Filters.Single() as FilterItemDto;
        Assert.That(filter, Is.Not.Null);
        Assert.That(filter!.Left, Is.TypeOf<FieldDto>());
        Assert.That(((FieldDto)filter.Left).FieldName, Is.EqualTo("Name"));
        Assert.That(filter.Right, Is.TypeOf<ConstFieldDto>());
        Assert.That(((ConstFieldDto)filter.Right!).Value, Is.EqualTo("Alice"));

        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var serialized = System.Text.Json.JsonSerializer.Serialize(definition, options);
        Assert.That(serialized, Does.Contain("\"left\":{\"kind\":\"field\",\"field\":\"Name\"}"));
        Assert.That(serialized, Does.Contain("\"right\":{\"kind\":\"value\",\"value\":\"Alice\"}"));
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<QueryDto>(serialized, options)!;
        Assert.That(((FieldDto)((FilterItemDto)roundTrip.Filter!.Filters.Single()).Left).FieldName, Is.EqualTo("Name"));
    }

    [Test]
    public void DefinitionTextUsesFieldDtoLiteralSyntax()
    {
        var definition = QueryDto.FromJson(
            """{"entity":"User","filter":{"logicalOperation":0,"filters":[{"logicalOperator":0,"field":{"kind":"field","field":"Name"},"operation":0,"value":"Alice Smith","parameterized":true}]}}""");

        Assert.That(definition.ToString(), Does.Contain("Filters: Name = <Alice Smith>"));
    }

    [Test]
    public void DefinitionJsonSupportsLegacyStringFilterFields()
    {
        var definition = QueryDto.FromJson(
            """{"entity":"User","filter":{"logicalOperation":0,"filters":[{"logicalOperator":0,"field":"Name","operation":0,"value":"Alice"}]}}""");

        Assert.That(((FieldDto)((FilterItemDto)definition.Filter!.Filters.Single()).Left).FieldName, Is.EqualTo("Name"));
    }

    [Test]
    public void JsonDefinitionSupportsAliasesLikeGroupingSortingAndPaging()
    {
        const string json = """
            {
              "entity": "User",
              "fields": [
                { "field": "Name" },
                { "field": "Manager.Name", "alias": "Manager-Name" }
              ],
              "filter": "[Name] like 'Dav%' && ([Manager.Name] == 'Alice' || [Members.TotalMembers] < 12)",
              "sorts": ["-Manager.Name"],
              "page": 1,
              "pageSize": 50
            }
            """;

        var sql = _db.QueryJson(json).ToSql();

        Assert.That(sql.Parameters.Select(parameter => parameter.Value),
            Is.EquivalentTo(new object?[] { "Dav%", "Alice", 12 }));

        var data = _db.QueryJson(json).ToList();

        string[] expectedValues = ["David - Bob"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Manager-Name"] ?? "NULL"}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void JsonDefinitionRejectsInvalidFilter()
    {
        const string json = """{"entity":"User","filter":"Name like"}""";

        Assert.Throws<FormatException>(() => QueryDto.FromJson(json));
        Assert.Throws<FormatException>(() =>
            QueryDto.FromJson("""{"entity":"User","filter":"Age > (12 + OtherAge)"}"""));
    }

    [Test]
    public void JsonDefinitionAcceptsUnquotedStringFilterValues()
    {
        var definition = QueryDto.FromJson(
            """{"entity":"User","filter":"Name == Alice"}""");

        var sql = _db.Query(definition).ToSql();
        var records = _db.Query(definition).ToList();

        Assert.That(sql.Parameters.Select(parameter => parameter.Value), Is.EqualTo(new object?[] { "Alice" }));
        Assert.That(records.Select(record => record["Name"]), Is.EqualTo(new object?[] { "Alice" }));
    }

    [Test]
    public void JsonDefinitionParsesEscapedSingleQuotesInFilterValues()
    {
        var definition = QueryDto.FromJson("""{"entity":"User","filter":"Name == 'O''Neil'"}""");
        var filter = (FilterItemDto)definition.Filter!.Filters[0];

        Assert.That(((ConstFieldDto)filter.Right!).Value, Is.EqualTo("O'Neil"));
    }

    [Test]
    public void FilterTextRoundTripsDtoOperandsAndLogicalGroups()
    {
        var definition = QueryDto.FromJson(
            """{"entity":"User","filter":"Name = \"Alice Smith\" AND (Age >= 18 OR Name = [Manager.Name])"}""");
        var text = definition.Filter!.ToText();
        var roundTrip = QueryDto.FromJson(
            System.Text.Json.JsonSerializer.Serialize(new { entity = "User", filter = text }));
        var outerFilters = ((FilterDto)roundTrip.Filter!).Filters;
        var fieldReferenceFilter = (FilterItemDto)((FilterDto)outerFilters[1]).Filters[1];

        Assert.That(text, Is.EqualTo(
            "Name = <Alice Smith> AND (Age >= <18> OR Name = Manager.Name)"));
        Assert.That(((ConstFieldDto)((FilterItemDto)outerFilters[0]).Right!).Value, Is.EqualTo("Alice Smith"));
        Assert.That(fieldReferenceFilter.Right, Is.TypeOf<FieldDto>());
        Assert.That(((FieldDto)fieldReferenceFilter.Right!).FieldName, Is.EqualTo("Manager.Name"));
        Assert.That(roundTrip.Filter!.ToText(), Is.EqualTo(text));
    }

    [Test]
    public void FilterTextParserDelegatesAngleValuesToFieldParser()
    {
        var cases = new (string Text, object? Value)[]
        {
            ("Age = <18>", 18),
            ("Age = <18.5>", 18.5m),
            ("Active = <true>", true),
            ("Name = <Alice Smith>", "Alice Smith"),
            ("Name = <>", null)
        };

        foreach (var (text, value) in cases)
        {
            var definition = QueryDto.FromJson(
                System.Text.Json.JsonSerializer.Serialize(new { entity = "User", filter = text }));
            var filter = (FilterItemDto)definition.Filter!.Filters.Single();

            Assert.That(((ConstFieldDto)filter.Right!).Value, Is.EqualTo(value), text);
        }

        var nullRightDefinition = QueryDto.FromJson(
            """{"entity":"User","filter":"Name = <NULL>"}""");
        Assert.That(
            ((FilterItemDto)nullRightDefinition.Filter!.Filters.Single()).Right,
            Is.Null);
    }

}
