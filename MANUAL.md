# dynamic-sql Manual

## Overview

`dynamic-sql` is a metadata-driven .NET query library. It executes queries
through ADO.NET while allowing entities, fields, relationships, projections,
filters, and aggregates to be defined at runtime.

The library targets .NET 8, .NET 9, and .NET 10.

## Installation

Add the NuGet package, or reference the projects while developing locally:

```xml
<ItemGroup>
  <PackageReference Include="Namh.Data.DynamicSql" Version="..." />
</ItemGroup>
```

The model types are included through the library package. A local project
reference looks like this:

```xml
<ProjectReference Include="path\to\src\Namh.Data.DynamicSql\Namh.Data.DynamicSql.csproj" />
```

## Define metadata

Metadata describes the database without requiring CLR entity classes:

```csharp
using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.Model;

var metadata = new[]
{
    new Entity(
        Name: "User",
        Fields:
        [
            new("Id", DataType.Number),
            new("Name"),
            new("Age", DataType.Number),
            new("DeletedOn", DataType.Datetime),
        ],
        Relations:
        [
            new(
                Name: "Manager",
                RelatedEntity: "User",
                Type: RelationType.Lookup,
                RelatedKeys: [new("ManagerId", "Id")]),
            new(
                Name: "Members",
                RelatedEntity: "User",
                Type: RelationType.Collection,
                RelatedKeys: [new("Id", "ManagerId")]),
        ])
};

var metaProvider = new MetaProvider(metadata);
```

### Entities

```csharp
new Entity(
    Name: "Student",
    RawSql: "[User]",
    Fields:
    [
        new("Name"),
        new("Age", DataType.Number),
    ]);
```

`Name` is the logical entity name used by queries and relationships.
`RawSql` is the physical database table expression used when generating SQL.
If only one is provided, the missing value is copied from the provided value.
When both are provided, they remain independent.

### Fields

Fields can be:

- `FieldType.DbColumn`: a normal database column.
- `FieldType.Aggregate`: an aggregate such as `Count(*)` or `Sum(@Age)`.
- `FieldType.RawSql`: a trusted provider-specific SQL expression.

Supported metadata data types include `String`, `Number`, `Datetime`, and
`Boolean`.

### Relationships

Use `RelationType.Lookup` for one related row and
`RelationType.Collection` for one-to-many traversal:

```csharp
new Relation(
    Name: "School",
    RelatedEntity: "School",
    Type: RelationType.Lookup,
    RelatedKeys: [new("SchoolId", "Id")]);
```

## Create a context

The context owns and disposes the supplied connection:

```csharp
using Microsoft.Data.SqlClient;

using var db = new DynamicSqlContext(
    new SqlConnection(connectionString),
    metaProvider);
```

The dialect is inferred from the connection type. It can also be supplied
explicitly:

```csharp
using var db = new DynamicSqlContext(
    connection,
    metaProvider,
    new SqliteDialect(schema: "dbo"));
```

Built-in dialects are `SqlServerDialect`, `SqliteDialect`, and
`PostgresDialect`. PostgreSQL uses the `public` schema by default.

Dialects control identifier quoting, booleans, string concatenation, and
paging. Raw SQL and aggregate expressions must use syntax supported by the
configured provider.

## C# queries

Results are returned as `DataRecord`, a case-insensitive field/value map:

```csharp
var users = db.Query("User")
    .Where(user => user["Age"] >= 18)
    .Select("Id", "Name", "Manager.Name")
    .OrderBy("Name")
    .ToList();
```

```csharp
foreach (var user in users)
    Console.WriteLine($"{user["Id"]}: {user["Name"]}");
```

### Filters

```csharp
var users = db.Query("User")
    .Where(user =>
        user["Age"] >= 18 &&
        user["Name"].Like("Dav%"))
    .ToList();
```

Use `RawCompare` only with trusted, application-controlled SQL fragments:

```csharp
var users = db.Query("User")
    .Where(user => user["Name"].RawCompare("like 'A%'"))
    .ToList();
```

### Projections and aliases

```csharp
var users = db.Query("User")
    .Select(new DataRecord
    {
        ["Id"] = "Id",
        ["DisplayName"] = "Name",
        ["ManagerName"] = "Manager.Name",
    })
    .ToList();
```

Computed projections can use related queries:

```csharp
var users = db.Query("User")
    .Select(user => new DataRecord
    {
        ["Name"] = user["Name"],
        ["MemberCount"] = user.Query("Members").Count(),
    })
    .ToList();
```

### Relationships and subqueries

```csharp
var managers = db.Query("User")
    .Where(user => user.Query("Members")
        .Any(member => member["Age"] > 30))
    .Select("Name", "Members.Name")
    .ToList();

var activeManagers = db.Query("User")
    .Where(user => user.Query("Members").Count() >= 2)
    .ToList();
```

### Ordering and paging

```csharp
var page = db.Query("User")
    .Where(user => user["IsMale"] == true)
    .OrderByDescending("Age")
    .ThenBy("Name")
    .Skip(10)
    .TakeQuery(10)
    .ToList();
```

`TakeQuery` keeps paging composable. `Take` materializes a list.

To fetch a page together with the total number of matching records before
paging, use `ToListWithTotal`. The total is `-1` when more than 1,000 records
match:

```csharp
var query = db.Query("User")
    .Where(user => user["IsMale"] == true)
    .OrderByDescending("Age")
    .Skip(10)
    .TakeQuery(10);

var (pageRecords, resultTotal) = query.ToListWithTotal(out var totalRecords);
```

### Scalar operations

```csharp
var totalUsers = db.Query("User").Count();
var adults = db.Query("User").Count(user => user["Age"] >= 18);
var hasManagers = db.Query("User")
    .Any(user => user["ManagerId"] != null);
```

### Aggregates

If an aggregate field is defined in metadata:

```csharp
var totalAge = db.Query("User").Aggregate("TotalAges");
```

## JSON queries

Use `QueryJson` when a screen, API, or report supplies query rules at runtime:

```csharp
var rows = db.QueryJson("""
{
  "entity": "User",
  "fields": [
    { "field": "Name" },
    { "field": "Manager.Name", "alias": "Manager-Name" }
  ],
  "filter": "[Name] like 'Dav%' && ([Manager.Name] == 'Alice' || [Age] < 12)",
  "sorts": ["-Manager.Name"],
  "page": 1,
  "pageSize": 50
}
""").ToList();
```

Supported properties:

- `entity`: metadata entity name.
- `fields`: projection fields; each entry has a `field` expression and an
  optional `alias`. A field expression can be a field path string or a
  structured expression object.
- `groupBy`: field paths used as explicit group keys.
- `filter`: field comparisons, `like`, `&&`, `||`, `AND`, `OR`, and parentheses.
- `sorts`: field paths; prefix with `-` for descending order.
- `page`: one-based page number.
- `pageSize`: number of rows per page.

Bare words on the right side of a filter comparison are accepted as string
values (for example, `Status == Paid`). Filter values remain parameterized by
the query engine, so the active provider handles their SQL representation.
Plan-text filters use `=`/`<>`, quote string values with double quotes, and
write right-side field references in brackets; this text can be parsed back
into the same filter structure.

Field references in filters use brackets:

```text
[Name]
[Manager.Name]
[Members.TotalMembers]
```

Values may be strings, integers, decimals, booleans, or `null`. JSON filter
values are parameterized by the query pipeline.

Calculated projections use a structured expression tree rather than SQL text.
Supported nodes are `field`, numeric `value`, arithmetic `binary` (`+`, `-`,
`*`, `/`), and `function` (`COUNT`, `SUM`, `AVG`, `ROUND`, `NULLIF`,
`DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`,
`DateFormat`, `GETDATE`):

```json
{
  "entity": "Products",
  "fields": [
    { "field": { "kind": "field", "field": "Name" } },
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
    }
  ],
  "groupBy": ["Id", "Name"],
  "sorts": ["-GrossProfit"]
}
```

Related field references use metadata relation paths, so the query compiler
creates the necessary joins. Numeric expression values are parameterized.
Function calls are validated against definitions in the metadata provider.
The default provider includes the built-in functions shown above. Applications can add or override definitions with `FunctionDefinition`.
Each definition specifies its UI-facing arguments, result type, and either a
generic SQL template or templates keyed by the dialect's stable string
identifier (`ISqlDialect.Key`). A dialect-specific template takes precedence;
the generic template is used when no provider override is defined. Functions
whose syntax is provider-specific can omit the generic template and fail
explicitly when used with an unsupported dialect. Templates are trusted
metadata, not query input. They use 1-based argument placeholders
(`{1}`, `{2}`, ...), while query values remain parameterized.
`FunctionDefinition.Description` is required and provides UI-friendly help
text for each function, and `FunctionDefinition.Arguments` describes its
inputs. Built-in
date helpers use readable names while their templates use each provider's
native SQL:

```csharp
var daysBetween = new FunctionFieldDto("DateSub",
[
    new FieldDto("InvoiceDate"),
    new FieldDto("PaidOn")
]);

var dueInTwoMonths = new FunctionFieldDto("MonthAdd",
[
    new FieldDto("DueDate"),
    new ConstFieldDto(2)
]);

var monthLabel = new FunctionFieldDto("DateFormat",
[
    new FieldDto("InvoiceDate"),
    new ConstFieldDto("%Y-%m")
]);
```

`DateSub(startDate, endDate)` returns elapsed whole days;
`MonthSub` and `YearSub` return calendar-boundary differences. `DateAdd`,
`MonthAdd`, and `YearAdd` add the specified number of units to a date.
`DateFormat` is currently SQLite-specific and supports `%Y-%m` (year-month)
and `%w` (weekday number). The provider-specific SQL remains internal; for
example, `DateSub` uses `DATEDIFF(day, ...)` on SQL Server.

`SqlTypeDefinition` uses the same provider-keyed override and generic fallback
pattern for logical SQL types. `IMetaProvider.GetFunctions()` and
`GetSqlTypes()` expose the registered definitions for applications that build
a function-selection UI.

Adding a database provider does not require changes to a fixed dialect enum.
Implement `ISqlDialect` with a unique `Key`, then supply or override metadata
definitions for functions and logical SQL types whose syntax differs. For
example, an Oracle-specific function can be registered as:

```csharp
var toDate = new FunctionDefinition(
    name: "TO_DATE",
    arguments: [FunctionArgumentDefinition.QueryExpression("value")],
    resultType: typeof(DateTime),
    description: "Parses a date from a string.",
    sqlTemplates: new Dictionary<string, string>
    {
        ["Oracle"] = "TO_DATE({1}, 'YYYY-MM-DD')"
    });

var metadata = new MetaProvider(entities, functions: [toDate]);
```

For example, an application can expose a one-argument `TODAY(value)` wrapper
and keep the SQL type fixed in the definition:

```csharp
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

var metadata = new MetaProvider(entities, functions: [today]);
using var context = new DynamicSqlContext(connection, metadata, dialect);
```

The function call still receives an expression DTO, such as
`new FunctionFieldDto("TODAY", [new FieldDto("InvoiceDate")])`. Nested
functions and arithmetic expressions can be supplied as arguments. Built-in
date function templates map friendly function names to provider-specific SQL,
and `GETDATE()` renders the provider's current date and time.

## SQL diagnostics

Inspect generated SQL without executing it:

```csharp
var sql = db.QueryJson(json).ToSql();

Console.WriteLine(sql.CommandText);

foreach (var parameter in sql.Parameters)
    Console.WriteLine($"{parameter.Name} = {parameter.Value}");
```

For SQL Server, `CommandText` starts with typed declarations:

```sql
DECLARE @p0 nvarchar(4000) = 'Dav%',
	@p1 nvarchar(4000) = 'Alice',
	@p2 int = 12;

SELECT ...
```

This output can be copied directly into a SQL Server query window. Other
dialects include a trailing parameter comment.

Treat diagnostic output as sensitive because it may contain user data.

## Async APIs

Async list, first-row, count, and existence operations accept cancellation:

```csharp
var users = await db.Query("User")
    .Where(user => user["Age"] >= 18)
    .ToListAsync(cancellationToken);

var (pageRecords, totalRecords) = await db.Query("User")
    .Where(user => user["Age"] >= 18)
    .Skip(10)
    .TakeQuery(10)
    .ToListWithTotalAsync(cancellationToken);

var firstUser = await db.Query("User")
    .FirstOrDefaultAsync(cancellationToken);

var adultCount = await db.Query("User")
    .CountAsync(user => user["Age"] >= 18, cancellationToken);

var hasManagers = await db.Query("User")
    .AnyAsync(user => user["ManagerId"] != null, cancellationToken);
```

## Archive behavior

Entities with a `DeletedOn` field exclude archived rows by default:

```csharp
var allUsers = db.Query("User", includeArchive: true).ToList();
```

The convention applies to the root and related queries.

## Raw SQL and security

`DbColumn` and `RawSql` are escape hatches for trusted, provider-specific
expressions:

```csharp
var query = db.Query("User")
    .Where(user => user.DbColumn("Id") > 100)
    .Where(user => user.RawSql("DATEDIFF(day, [Birthday], GETDATE())") > 3650);
```

Never interpolate untrusted input into `RawSql` or `RawCompare`. Normal
comparison values are parameterized.

## Build and test

```powershell
dotnet build .\src\Namh.Data.DynamicSql.Test\Namh.Data.DynamicSql.Test.csproj
dotnet test .\src\Namh.Data.DynamicSql.Test\Namh.Data.DynamicSql.Test.csproj
dotnet build .\src\Namh.Data.DynamicSql.ConsoleTest\Namh.Data.DynamicSql.ConsoleTest.csproj
```

SQL Server tests expect a local `DynamicSqlTest` database. SQLite and
PostgreSQL scripts are included in `scripts`.

## License

Copyright (c) 2026 NamHqUs

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
