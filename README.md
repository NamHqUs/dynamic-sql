# dynamic-sql

Build powerful database screens without creating a CLR class for every table.

`dynamic-sql` turns runtime metadata, C# expressions, or YAML definitions into
safe, provider-specific SQL. The same query engine can power a data grid,
report builder, admin screen, or API endpoint even when the fields and
relationships are selected at runtime.

## A query defined by data

```csharp
var rows = db.QueryYml("""
Entity: User
Projection:
  - Name
  - Manager.Name | Manager-Name
Filter: "[Name] like 'Dav%' && [Age] >= 18"
Sorts: Manager.Name DESC
Page: 1
PageSize: 50
""").ToList();
```

No generated entity class. No hand-written join. No string-built SQL.

The same metadata can be queried from C#:

```csharp
var rows = db.Query("User")
    .Where(user => user["Age"] >= 18)
    .Select("Name", "Manager.Name")
    .ToList();
```

## Why it is different

- Runtime entities, fields, relationships, and aggregates.
- One compiler for C# expressions and YAML queries.
- Relationship paths such as `Manager.Name` and `Members.Name`.
- Dynamic projections and aliases without CLR models.
- SQL Server, SQLite, PostgreSQL, Firebird, MySQL/MariaDB, Oracle, SAP HANA,
  and IBM Db2 dialect support.
- Parameterized filters and executable SQL diagnostics.
- A dialect-independent SQL expression intermediate representation.

```csharp
var sql = db.QueryYml(yml).ToSql();
Console.WriteLine(sql.CommandText);
```

For SQL Server, the diagnostic output includes typed `DECLARE` statements, so
the complete text can be copied directly into a query window.

## Start here

- Read the [user manual](https://github.com/NamHqUs/dynamic-sql/blob/main/MANUAL.md) for installation, metadata, queries, YAML,
  providers, async APIs, and troubleshooting.
- Read the [technical guide](https://github.com/NamHqUs/dynamic-sql/blob/main/TECH.md) for the compiler pipeline and important
  classes.
- See the [query model reference](https://github.com/NamHqUs/dynamic-sql/blob/main/QUERY-MODEL-REFERENCE.md) for the
  canonical backend JSON format for projections and query contexts.

Targets: **.NET 8, .NET 9, and .NET 10**.

## Run the demo

Install the **.NET 10 SDK**, **Node.js with npm**, and **Windows Terminal**,
then run this from the repository root in PowerShell:

```powershell
.\start.ps1
```

The script installs frontend dependencies on first run, then opens the API and
frontend in separate, titled Windows Terminal tabs, in that order. The API's
Swagger UI is at <http://localhost:21210/swagger>; the frontend is at
<http://localhost:5173>.

The demo backend is split into four projects: `Demo.WebApi` hosts the API and
registers dependencies, `Demo.Services` contains application logic,
`Demo.Contracts` defines service and repository abstractions, and
`Demo.Repositories` implements file, in-memory, and database access. Set
`StorageProvider` in `src/Demo.WebApi/appsettings.json` to `Files` (default) or
`Memory` to select the metadata, function-definition, and report store. Reports
are exposed through the `/report` API. Built-in metadata and SQL functions are
shared, read-only definitions; built-in reports are loaded from the existing
query-request storage folder
`src/Demo.WebApi/FileStorage/QueryRequests/*.yml` and can be edited and run but
not saved or deleted. Custom metadata, functions, and reports are stored
under `FileStorage/UserData/{SectionId}/` and are isolated by section.
Database connections remain selected separately through `DbServerType`.
The `IQueryRequestRepository` stores `QueryDefinition` YAML files in
`FileStorage/QueryRequests` for built-ins and
`FileStorage/UserData/{SectionId}/QueryRequests` for custom requests. These
files use the flat `QueryDefinition` YAML shape (for example, `Entity` and
`Fields` are top-level properties).

## License

MIT License. See [the manual](https://github.com/NamHqUs/dynamic-sql/blob/main/MANUAL.md#license) for the full license text.
