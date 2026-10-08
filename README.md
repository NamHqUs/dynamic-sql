# dynamic-sql

`dynamic-sql` is a metadata-driven .NET query library for building SQL queries
against an existing relational database. It exposes a small LINQ-like API
while keeping the entity model dynamic: fields, relationships, computed
expressions, and aggregate fields are described at runtime rather than mapped
to CLR entity classes.

The library currently targets **.NET 8, .NET 9, and .NET 10**. It works with
ADO.NET providers that implement `System.Data.IDbConnection`; the repository's
tests exercise SQL Server and SQLite.

## Features

- Query tables or raw SQL entities by metadata name.
- Select fields by name, including nested relationship paths such as
  `Manager.Name` and `Members.Manager.Name`.
- Define lookup and collection relationships.
- Filter with strongly typed C# expressions, including nested subqueries with
  `Any` and `Count`.
- Project aliases and computed values into `DataRecord` results.
- Order, skip, and take records.
- Execute `Count`, `Any`, and aggregate queries without materializing rows.
- Exclude archived rows by default when an entity has a `DeletedOn` field, or
  include them with `includeArchive: true`.
- Use provider-specific database columns or raw SQL expressions when required.

## Getting started

Add a project reference to the library and its metadata model:

```xml
<ItemGroup>
  <ProjectReference Include="path\to\src\Namh.Data.DynamicSql\Namh.Data.DynamicSql.csproj" />
</ItemGroup>
```

Create metadata for the entities that can be queried. Each `Entity` contains
its database name, fields, and optional relationships. A `Field` can represent
a normal database column, an aggregate, or a raw SQL field.

```csharp
using Namh.Data.DynamicSql;
using Namh.Data.DynamicSql.Model;

var metadata = new List<Entity>
{
    new(
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
        ]),
};

var metaProvider = new MetaProvider(metadata);
```

Pass an `IDbConnection` and the metadata provider to `DynamicSqlContext`.
The context owns and disposes the connection when it is disposed.

```csharp
using Microsoft.Data.SqlClient;

using var db = new DynamicSqlContext(
    new SqlConnection(connectionString),
    metaProvider);
```

## Querying

Results are returned as `DataRecord`, a case-insensitive dictionary of field
names to values.

```csharp
var users = db.Query("User")
    .Where(user => user["Age"] >= 18 && user["Name"].RawCompare("like 'A%'"))
    .Select("Id", "Name", "Manager.Name")
    .OrderBy("Name")
    .ToList();

foreach (var user in users)
    Console.WriteLine($"{user["Id"]}: {user["Name"]} ({user["Manager.Name"] ?? "none"})");
```

### Projections and aliases

Use `Select` with field paths, a `DataRecord` alias map, or an expression that
builds a `DataRecord`.

```csharp
var users = db.Query("User")
    .Select(new DataRecord
    {
        ["Id"] = "Id",
        ["DisplayName"] = "Name",
        ["ManagerName"] = "Manager.Name",
    })
    .ToList();

var computed = db.Query("User")
    .Select(user => new DataRecord
    {
        ["Name"] = user["Name"],
        ["MemberCount"] = user.Query("Members").Count(),
    })
    .ToList();
```

### Relationships and subqueries

Relationship paths can be used directly in projections and predicates.
Collection relationships can also be queried as subqueries.

```csharp
var managers = db.Query("User")
    .Where(user => user.Query("Members")
        .Any(member => member["Age"] > 30))
    .Select("Name", "Members.Name")
    .ToList();

var usersWithMembers = db.Query("User")
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
    .Take(10);
```

`Take` materializes a `List<DataRecord>`. `ToList` materializes the current
query, while `FirstOrDefault` returns the first result or `null` when no row
matches.

### Scalar operations

```csharp
var totalUsers = db.Query("User").Count();
var adults = db.Query("User").Count(user => user["Age"] >= 18);
var hasManagers = db.Query("User")
    .Any(user => user["ManagerId"] != null);
```

Async equivalents are available for list, first-row, count, and existence
queries. They accept an optional `CancellationToken`:

```csharp
var users = await db.Query("User")
    .Where(user => user["Age"] >= 18)
    .ToListAsync(cancellationToken);

var firstUser = await db.Query("User")
    .FirstOrDefaultAsync(cancellationToken);

var adultCount = await db.Query("User")
    .CountAsync(user => user["Age"] >= 18, cancellationToken);

var hasManagers = await db.Query("User")
    .AnyAsync(user => user["ManagerId"] != null, cancellationToken);
```

To execute an aggregate field defined in metadata, call `Aggregate` with its
field name:

```csharp
var totalAge = db.Query("User").Aggregate("TotalAges");
```

## Metadata reference

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

When `RawSql` is omitted, the entity is rendered as `dbo.[EntityName]`.
`RawSql` can instead point to a table, view, or derived query.

### Fields

`Field` supports these types:

- `FieldType.DbColumn` (default): a normal database column.
- `FieldType.Aggregate`: an aggregate expression such as `Count(*)` or
  `Sum(@Age)`.
- `FieldType.RawSql`: a custom SQL expression.

`DataType` can be `String`, `Number`, `Datetime`, or `Boolean`.

### Relationships

Use `RelationType.Lookup` for a single related row and
`RelationType.Collection` for one-to-many traversal. Each `RelationItem`
maps a source database column to a related database column:

```csharp
new Relation(
    Name: "School",
    RelatedEntity: "School",
    Type: RelationType.Lookup,
    RelatedKeys: [new("SchoolId", "Id")]);
```

## Raw SQL and provider-specific expressions

`DbColumn` and `RawSql` are escape hatches for database-specific queries:

```csharp
var query = db.Query("User")
    .Where(user => user.DbColumn("Id") > 100)
    .Where(user => user.RawSql("DATEDIFF(day, [Birthday], GETDATE())") > 3650);
```

Only use raw SQL with trusted, application-controlled strings. Values supplied
through normal comparisons are translated as query parameters; interpolating
untrusted input into `RawSql` or `RawCompare` can create SQL injection risk and
may make a query provider-specific.

## Archive behavior

By default, queries exclude rows considered archived. To include archived rows
for the root entity and related queries, pass `includeArchive: true`:

```csharp
var allUsers = db.Query("User", includeArchive: true).ToList();
```

The archive convention is based on the metadata/database `DeletedOn` field.

## Building and testing

From the repository root:

```powershell
dotnet build .\src\Namh.Data.DynamicSql.Test\Namh.Data.DynamicSql.Test.csproj
dotnet test .\src\Namh.Data.DynamicSql.Test\Namh.Data.DynamicSql.Test.csproj
dotnet build .\src\Namh.Data.DynamicSql.ConsoleTest\Namh.Data.DynamicSql.ConsoleTest.csproj
```

The test project includes SQLite-based test dependencies and also contains
SQL Server coverage. SQL Server tests expect a local `DynamicSqlTest` database
and a connection configured by the test project.

## Project layout

- `src/Namh.Data.DynamicSql` - query context, provider, expressions, and
  extension methods.
- `src/Namh.Data.DynamicSql.Model` - entity, field, and relationship metadata.
- `src/Namh.Data.DynamicSql.Test` - NUnit tests and database fixtures.
- `src/Namh.Data.DynamicSql.ConsoleTest` - console usage sample.
- `scripts/db-scripts.sql` - sample schema and seed data.
- `docker` - SQL Server container helpers.

## License

This project is licensed under the MIT License.

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
