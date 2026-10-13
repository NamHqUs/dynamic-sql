# dynamic-sql

Build powerful database screens without creating a CLR class for every table.

`dynamic-sql` turns runtime metadata, C# expressions, or JSON definitions into
safe, provider-specific SQL. The same query engine can power a data grid,
report builder, admin screen, or API endpoint even when the fields and
relationships are selected at runtime.

## A query defined by data

```csharp
var rows = db.QueryJson("""
{
  "entity": "User",
  "columns": ["Name", "Manager.Name|Manager-Name"],
  "filter": "[Name] like 'Dav%' && [Age] >= 18",
  "sorts": ["-Manager.Name"],
  "page": 1,
  "pageSize": 50
}
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
- One compiler for C# expressions and JSON queries.
- Relationship paths such as `Manager.Name` and `Members.Name`.
- Dynamic projections and aliases without CLR models.
- SQL Server, SQLite, and PostgreSQL dialect support.
- Parameterized filters and executable SQL diagnostics.
- A dialect-independent SQL expression intermediate representation.

```csharp
var sql = db.QueryJson(json).ToSql();
Console.WriteLine(sql.CommandText);
```

For SQL Server, the diagnostic output includes typed `DECLARE` statements, so
the complete text can be copied directly into a query window.

## Start here

- Read the [user manual](MANUAL.md) for installation, metadata, queries, JSON,
  providers, async APIs, and troubleshooting.
- Read the [technical guide](TECH.md) for the compiler pipeline and important
  classes.

Targets: **.NET 8, .NET 9, and .NET 10**.

## Run the demo

Install the **.NET 10 SDK** and **Node.js with npm**, then run this from the
repository root in PowerShell:

```powershell
.\start.ps1
```

The script installs frontend dependencies on first run, then starts the API and
frontend in separate windows. Open the frontend at
<http://localhost:5173>; the API's Swagger UI is at
<http://localhost:21210/swagger>.

## License

MIT License. See [MANUAL.md](MANUAL.md#license) for the full license text.
