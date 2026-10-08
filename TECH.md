# dynamic-sql Technical Guide

## Architecture

The query compiler is intentionally split into provider-independent translation
and provider-specific rendering:

```text
C# expression / JSON definition
              ↓
Expression visitors
              ↓
SqlExpression IR
              ↓
SqlGenerator(ISqlDialect)
              ↓
Generated SQL + parameters
```

The visitors never own a dialect. They create an intermediate representation
that can be rendered for SQL Server, SQLite, PostgreSQL, or another dialect.

## Projects

```text
src/Namh.Data.DynamicSql
    Public query API, context, dialects, provider, visitors, IR, generator

src/Namh.Data.DynamicSql.Model
    Entity, Field, Relation, QueryDefinition, and JSON filter models

src/Namh.Data.DynamicSql.Test
    NUnit tests, metadata fixtures, provider tests, and SQL diagnostics tests

src/Namh.Data.DynamicSql.ConsoleTest
    Manual usage sample
```

## Important public classes

### `DynamicSqlContext`

Creates query providers from an `IDbConnection`, metadata provider, and
optional dialect:

```csharp
var db = new DynamicSqlContext(connection, metadata, dialect);
```

It exposes:

- `Query(entityName)`
- `Query(entityName, includeArchive)`
- `QueryJson(json)`

The context owns the supplied connection.

### `IRecordParameter`

The expression-tree surface for dynamic field access:

```csharp
query.Where(record => record["Name"] == "Alice");
```

It provides:

- Indexer field access
- `DbColumn`
- `RawSql`
- Related query access through `Query`

`DataValue` overloads make comparisons and expressions possible without
materializing a CLR entity.

### `QueryDefinition`

The JSON transport model contains entity, columns, filters, sorting, and
paging. `QueryDefinitionExtensions` converts it into the same query pipeline
used by C# expressions.

`QueryDefinitionJsonParser` is responsible for:

- JSON property parsing
- Column aliases
- Bracketed field paths
- Filter precedence
- Typed literals
- Arithmetic filter values
- Sort directions

JSON values are marked as parameterized before expression translation.

## Query provider flow

### `DbQueryProvider`

`DbQueryProvider` connects the public query object to translation and execution.
It:

1. Creates a `QueryContext`.
2. Visits the expression tree with `QueryVisitor`.
3. Generates SQL with `SqlGenerator`.
4. Creates ADO.NET parameters.
5. Executes synchronous or asynchronous commands.

`GetQuery` is used for diagnostics and returns `SqlQuery`.

### `QueryContext`

`QueryContext` owns translation state:

- Metadata lookup
- Table aliases
- Relationship joins
- SQL parameters
- Archive filters
- Field-path resolution

It must remain dialect-independent. Dialect decisions belong in
`SqlGenerator` and `ISqlDialect`.

### Visitors

Important visitors include:

- `QueryVisitor`: visits the overall query expression.
- `ClauseVisitor`: handles `Where`, `Select`, ordering, paging, `Count`, and
  `Any`.
- `WhereVisitor`: translates predicates and parameter values.
- `SelectVisitor`: translates projections and `DataRecord` aliases.
- `RecordParameterVisitor`: translates field, raw SQL, database-column, and
  related-query access.
- `LambdaVisitor`: common lambda traversal behavior.

Visitors produce IR nodes rather than SQL strings.

## SQL expression IR

The IR is in `Internal/Query/Expressions`. Important types include:

- `SqlExpression`: base type for dialect-independent SQL nodes.
- `TableExpression`: query scope, joins, filters, projections, ordering, and
  paging.
- `FieldExpression`: metadata-backed field reference.
- `ValueExpression`: literal or parameter placeholder.
- `BinarySqlExpression`: binary operations.
- `PredicateExpression`: predicates such as `LIKE`.
- `CompareSqlExpression`: raw comparison operators.
- `ProjectionExpression`: aliased projection.
- `AggregateExpression`: aggregate metadata and subqueries.
- `SubqueryExpression`: nested query.
- `AnyExpression`: `EXISTS` query.
- `JoinExpression`: relationship join and join conditions.

The IR must not depend on `ISqlDialect`.

## `SqlGenerator`

`SqlGenerator` consumes a `TableExpression` and an `ISqlDialect`. It renders:

- `SELECT`
- `FROM`
- joins
- `WHERE`
- `ORDER BY`
- provider-specific paging
- field and identifier quoting
- aggregate subqueries
- parameters

It also preserves readable diagnostics formatting. SQL execution uses the
generated command text and parameter collection directly; it does not use the
diagnostic formatter.

## `ISqlDialect`

The dialect abstraction owns provider differences:

- Identifier quoting
- Boolean rendering
- String concatenation
- `TOP`, `LIMIT`, or `OFFSET/FETCH`
- Schema/table rendering

Built-in implementations:

- `SqlServerDialect`
- `SqliteDialect`
- `PostgresDialect`

When adding a provider, implement `ISqlDialect` and add SQL-generation tests
for identifiers, booleans, concatenation, and paging.

## Diagnostics formatting

`SqlFormatter` formats diagnostic SQL only. It does not participate in normal
execution.

For SQL Server diagnostics, `DbQueryProvider.GetQuery` prepends typed
declarations:

```sql
DECLARE @p0 nvarchar(4000) = 'Dav%',
	@p1 nvarchar(4000) = 'Alice',
	@p2 int = 12;
```

The actual command still uses `@p0`, `@p1`, and `@p2`, while
`SqlQuery.Parameters` retains the structured values.

## Metadata resolution

`QueryContext.ParseFieldPath` resolves paths such as:

```text
Name
Manager.Name
Members.TotalMembers
```

It follows relations, creates aliases, applies archive filters, and resolves
aggregate fields. Missing entities, fields, and relations produce dedicated
metadata exceptions:

- `EntityNotFoundException`
- `FieldNotFoundException`
- `RelationNotFoundException`

## Adding a query feature

Follow this sequence:

1. Add or reuse a public query method.
2. Add the corresponding visitor translation.
3. Represent the result with a dialect-independent IR node.
4. Add rendering in `SqlGenerator`.
5. Add provider-independent and provider-specific tests.
6. Add diagnostics assertions when output shape matters.
7. Update [MANUAL.md](MANUAL.md) for user-facing behavior.

Do not build provider-specific SQL inside visitors or `QueryContext`.

## Security boundaries

Normal values must flow through `QueryContext.AddDbParameter`. Do not inline
untrusted values into raw SQL. `RawSql`, `RawCompare`, and raw metadata
expressions are trusted escape hatches and must not receive user-controlled
text without validation.

## Testing

Run the complete suite:

```powershell
dotnet test .\src\Namh.Data.DynamicSql.Test\Namh.Data.DynamicSql.Test.csproj
```

The test project targets .NET 8, .NET 9, and .NET 10. Tests cover metadata
resolution, expression translation, JSON definitions, SQL diagnostics, async
execution, SQLite behavior, and SQL Server behavior.
