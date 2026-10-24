# NorthStar reports: Dynamic SQL implementation plan

## Goal and scope

Implement the 20 report inputs in
[northstar-query-input-catalog.md](./northstar-query-input-catalog.md) using
Dynamic SQL's own metadata and query-definition language, rather than shipping
them as hand-written SQL. Compare their results with
[northstar-demo-queries.md](./northstar-demo-queries.md). Keep the SQLite seed in
[northstar-demo.sqlite.sql](./northstar-demo.sqlite.sql) as the reference
fixture.

Authoring priority:

1. **YAML query definitions** are the preferred saved, reviewed, and edited
   report input.
2. **Plan text** is the readable rendering of a parsed `QueryDefinition` via
   `QueryDefinition.ToYml()` / `ToString()`. It should be used in examples,
   code review, diagnostics, and the report builder's text view. It is not a
   separate physical SQL plan or a new parser format.
3. **JSON** remains supported for existing clients and saved definitions, but
   it is not the authoring format for this demo.

The goal is not to add one-off SQL strings for each report. Add reusable
expression/query capabilities only where the reports demonstrate an important
general use case, then express each report as validated YAML metadata.

## Scenario metadata status

Built-in metadata for the 16 NorthStar tables has been appended in
[`BuiltinMetadata.cs`](../src/Demo.Share/BuiltinMetadata.cs). It describes
fields and lookup/collection relationships across people, employees,
departments, clinics, sales, visits, plans, claims, and HR.

The legacy sales demo already registers a different `Products` entity. The
NorthStar product metadata is therefore named `NorthStarProducts` and maps to
the physical `Products` table with `RawSql`. This keeps `MetaProvider`'s
case-insensitive entity-name dictionary unique and preserves the older demo.
NorthStar queries must refer to `OrderItems.Product` / `NorthStarProducts`,
not the legacy `Products` entity.

The seed is synthetic and dates are fixed in 2025. Use it as repeatable demo
data, not as actual patient, employee, or customer records.

## Implemented NorthStar input support

The 20 saved Query Builder requests are stored as JSON under
[`UserData/123/QueryRequests`](../src/Demo.WebApi/FileStorage/UserData/123/QueryRequests).
The catalog remains the YAML/plan-text authoring view requested for review;
JSON is used for persisted requests because that is the current saved-request
contract.

NorthStar-specific function definitions are registered in
[`CustomFunctionDefintions.cs`](../src/Demo.Share/CustomFunctionDefintions.cs).
They cover distinct and conditional aggregates, `COALESCE`, `CONCAT`, date
helpers, and the SA5 trend expressions. Predicate arguments are compiled as
typed filters, correlated aggregate paths support computed expressions, and
group expressions can be nested as arithmetic operands. SA5 uses provider
templates fixed to ascending, unpartitioned windows; it does not add a
general-purpose typed window model to `QueryDefinition`.

[`37.NorthStarQueryExecutionTests.cs`](../src/Namh.Data.DynamicSql.Test/UnitTest/37.NorthStarQueryExecutionTests.cs)
loads the saved requests, executes them against the SQLite seed, compares their
10-row result sets with the reference reports, checks registered provider
templates, and verifies SQL generation for SQLite, SQL Server, and PostgreSQL.

## Current input and compilation path

The existing path is:

```text
YAML text
  -> YamlDotNet deserializes YamlQueryDefinition
  -> QueryDefinitionParser.Parse(...) validates and parses expressions
  -> QueryDefinition (internal normalized query model)
  -> DbQueryFromQueryDto builds the IQueryable-style expression
  -> QueryVisitor / ClauseVisitor / SelectVisitor / WhereVisitor
  -> QueryContext resolves metadata, joins, aggregates, and parameters
  -> SQL expression IR (TableExpression, FieldExpression, aggregate,
     predicate, function, subquery, and related nodes)
  -> SqlStatementGenerator + ISqlDialect
  -> parameterized SQL and DataRecord projection
```

Relevant code:

- [`YamlQueryDefinition.cs`](../src/Namh.Data.DynamicSql/Contracts/QueryDefinitions/YamlQueryDefinition.cs)
  is a nullable deserialization DTO. It is not the normalized query model.
- [`QueryDefinition.cs`](../src/Namh.Data.DynamicSql/Contracts/QueryDefinitions/QueryDefinition.cs)
  is the normalized public model and writes its supported fields through
  `ToYml()`.
- [`QueryDefinitionParser.cs`](../src/Namh.Data.DynamicSql/Contracts/QueryDefinitions/Parsers/QueryDefinitionParser.cs)
  parses YAML, JSON, projection, filter, sort, and relation definitions.
- [`DbQueryFromQueryDto.cs`](../src/Namh.Data.DynamicSql/Execution/DbQueryFromQueryDto.cs)
  converts a `QueryDefinition` to the existing query-builder expression flow.
- [`QueryContext.cs`](../src/Namh.Data.DynamicSql/Compilation/QueryContext.cs)
  resolves metadata paths, adds joins, and parameterizes literal values.
- [`SqlStatementGenerator.cs`](../src/Namh.Data.DynamicSql/Compilation/SqlStatementGenerator.cs)
  renders the dialect-independent SQL expression model.
- [`SqlDialect.cs`](../src/Namh.Data.DynamicSql/Contracts/SqlDialect.cs)
  owns provider quoting, paging, concatenation, and dialect-specific rendering.

The expression visitor API and the `QueryDefinition` API converge on the same
compiler. Visitors should continue producing SQL expression IR; provider SQL
belongs in the dialect/generator layer, not in expression parsing.

## Existing capabilities and gaps

### Usable now

- Root entity and related fields, with relationships resolved from built-in
  metadata. Relationship paths create joins automatically; built-in metadata
  joins are currently rendered as `LEFT JOIN`.
- Named independent relations for self-joins and alternate joins.
- Projection fields, aliases, arithmetic expressions, registered functions,
  filters, sorting, and paging.
- Parameterized filter values and standard comparisons, set/range predicates,
  null checks, and boolean groups.
- `COUNT`, `SUM`, `AVG`, `MIN`, and `MAX`; when a projection mixes aggregate
  and non-aggregate items, the generator infers grouping from the non-aggregate
  projection items.
- Aggregate filter predicates are routed to `HAVING`.
- `Any`/`EXISTS` and subquery nodes through the expression API. YAML does not
  currently have a direct subquery or CTE definition.
- Built-in functions include `ROUND`, `NULLIF`, `DateFormat`, and `GETDATE`,
  subject to the function's provider templates.

### Remaining model limitations

- `QueryDefinition` has no explicit `GroupBy`, `Having`, `Distinct`, or
  `Limit` properties. Grouping is inferred; aggregate filters become `HAVING`;
  `Page` / `PageSize` implement paging. Make any additions explicit and
  backward compatible.
- The core built-in set still has no native `COUNT(DISTINCT ...)`,
  conditional-aggregate node, or window-function/`OVER` node. The NorthStar
  demo registers reusable function templates for distinct/conditional
  aggregates and SA5; these are demo-specific extension metadata, not additions
  to the general built-in set.
- SA5's `LAG`, `Difference`, and `WINDOW_SUM` templates have fixed ascending
  order and no partition support. They express this monthly report but do not
  provide a general window API or same-select alias references.
- One-to-many joins can multiply rows and inflate sums or averages. However,
  `QueryContext` already has relationship aggregate handling that can render
  correlated scalar aggregates for paths such as `Members.TotalMembers`.
  Prefer those aggregates for independent child collections. Add pre-aggregate
  stages only when a required result grain cannot be expressed safely that
  way; `DISTINCT` on the final projection is not a safe fix.
- Date functions and string concatenation have SQLite, SQL Server, and
  PostgreSQL templates for this demo. Keep provider-specific syntax inside
  those templates; every new function needs a template for each supported
  dialect.
- `YamlQueryDefinition.Name` and `LegacyProjectionRows` currently do not flow
  into `QueryDefinitionParser.Parse`. Before using named report metadata or
  relying on legacy row-shaped YAML, verify the intended compatibility
  contract and add parser tests. Do not silently depend on those fields.

## Report-by-report feature map

The identifiers below use domain prefixes to keep the two domains with five
reports each distinct: `SA` sales, `HC` health care, `IN` insurance, `HR` human
resources. P1 reports use current capabilities; later priorities require the
reusable features identified in the table.

| ID | Reference report | Main DSL or compiler requirement |
|----|------------------|----------------------------------|
| SA1 | Category and channel profitability | Grouping by two dimensions; distinct order count |
| SA2 | Customer lifetime value and recency | Related item aggregates, distinct order count, fan-out-safe rollup |
| SA3 | Sales representative performance | Employee/location joins, computed line margin, distinct orders |
| SA4 | Best-selling products and margin | Product joins, computed aggregates, `ROUND` / `NULLIF` |
| SA5 | Monthly sales trend and momentum | Date bucketing, `LAG`, running window aggregate |
| HC1 | Diagnosis burden by clinic | Lab-rate conditional aggregate, distinct visits/patients |
| HC2 | Physician workload and care-quality scorecard | Left joins, distinct counts, fan-out-safe lab summary |
| HC3 | High-risk patient care profile | Age/date expression, conditional chronic count, fan-out-safe labs |
| HC4 | Lab abnormality patterns | Grouping by test/category and aggregate rate |
| HC5 | Visit-type utilization by month | Date bucketing and conditional no-show aggregate |
| IN1 | Denial analysis by plan and reason | Plan joins, grouped counts and amounts |
| IN2 | Plan profitability and medical loss ratio | Independent premium/claim aggregates and ratio |
| IN3 | Agent portfolio and premium collection | Conditional payment buckets and related aggregates |
| IN4 | Claims adjudication by plan | Conditional rates and date-difference aggregate |
| IN5 | Member utilization versus premium | Independent member-level claim and premium aggregates |
| HR1 | Department compensation and performance | Department joins, salary/review aggregation |
| HR2 | Manager span and team profile | Employee self-join and fan-out-safe review aggregation |
| HR3 | Workforce composition by location | Conditional employment-type counts and tenure calculation |
| HR4 | Leave demand and approval status | Department/leave/status grouping and `SUM(Days)` |
| HR5 | Performance and bonus trends | Department/year grouping and conditional high-rating rate |

## Prioritized delivery plan

### P0 — Validate the metadata foundation

1. Build `Demo.Share` for every target framework.
2. Construct a `MetaProvider` from `BuiltinMetadata.DefaultMetadata` and assert
   every entity name is unique (case-insensitively).
3. Add metadata tests for each NorthStar entity's physical name, fields,
   relation target, relation direction, and key mapping.
4. Compile small `QueryDefinition` examples for lookup, collection, self-join,
   and the `NorthStarProducts` physical-table mapping.
5. Keep the legacy `Customers`/`Products`/`Invoices` built-ins resolving as
   before.

### P1 — Prove current-language reports (completed)

The saved-query tests load all 20 requests and compare their results with the
reference SQL. The catalog keeps YAML/plan-text examples for review; the
reports remain user-defined requests rather than being added to the built-in
report list. Filter values remain parameterized.

### P2 — Add measures and correlated aggregate inputs (implemented for NorthStar)

The NorthStar demo function definitions provide templates for ordinary
functions including `COUNT_DISTINCT`, `COALESCE`, `CONCAT`, date helpers, and
SA5 trend expressions. Predicate arguments and SQL translation cover:

- `COUNT_IF(predicate)` and `COUNT_DISTINCT_IF(predicate, expression)`.
- `SUM_IF(predicate, expression)`, `AVG_IF(predicate, expression)`, and
  `MAX_IF(predicate, expression)`.
- Related-aggregate predicate evaluation, so filtering a child aggregate does
  not turn the root relationship join into a row-multiplying join.
- Null-safe ratio and date calculations needed by the report inputs.

Translation preserves the visitor-to-IR-to-dialect separation and does not
accept free-form SQL fragments as the general-purpose representation.

The NorthStar saved requests now use these registered functions and
relationship aggregates across all 20 reports. Continue adding focused edge
fixtures for join fan-out, null values, and empty related sets when extending
the function surface.

### P3 — Add staged aggregation only if required

First attempt every multi-branch report using the correlated relationship
aggregates already represented by `QueryContext`. This avoids a new SQL-shaped
CTE syntax when independent aggregate expressions are sufficient. If testing
shows a required intermediate grain cannot be expressed or translated
efficiently, add a typed derived-stage input with explicit source entity,
grouping keys, projected aliases, and join keys.

Any staged feature must preserve parameterization, metadata resolution,
aliasing, and provider translation. Do not add it solely to mirror the
reference SQL's CTE structure.

### P4 — Add a general window model (deferred)

Add a provider-aware window expression model for `LAG` and cumulative
aggregates, with explicit partition/order terms. The query generator may wrap
the grouped result internally; the report author should not have to author a
CTE just to use a window.

The NorthStar SA5 report currently uses fixed-template custom functions for
ascending, unpartitioned `LAG` and running sums. Add a typed window model only
when a broader use case requires partitioning, configurable frames, or
provider-aware ordering; do not treat the fixed demo templates as general
window support.

### P5 — Productize the YAML-first report experience (remaining)

1. Store reports as YAML definitions and parse them through
   `QueryDefinitionParser.ParseFromYml`.
2. Render normalized `QueryDefinition.ToYml()` plan text in report preview and
   diagnostics, including aliases and relations.
3. Keep JSON parsing and existing saved JSON inputs as compatibility paths;
   add round-trip tests to ensure JSON-to-model-to-YAML retains supported
   semantics.
4. Decide and test the intended `Name` and legacy projection-row behavior in
   `YamlQueryDefinition`.
5. Run every report against the SQLite fixture and add provider translation
   tests for SQL Server and PostgreSQL.

## Input contract examples

### YAML (authoritative report input)

This example uses current projection, relation, filter, aggregate, sort, and
paging syntax for a simple physician workload report:

```yaml
Entity: Visits
Projection:
  - Doctor.FirstName | PhysicianFirstName
  - Doctor.LastName | PhysicianLastName
  - Diagnosis.Name | Diagnosis
  - COUNT() | VisitCount
  - SUM(Charge) | TotalCharges
Filter: "[Status] = <Completed>"
Sorts: VisitCount DESC
Page: 1
PageSize: 10
```

Mixed aggregate and non-aggregate projections are grouped by the non-aggregate
fields by the current SQL generator. This example does not require JSON, raw SQL,
or a direct SQL `JOIN` declaration.

### Plan text (reviewable normalized definition)

Parse the YAML into `QueryDefinition`, then use `ToYml()` to display or persist
the normalized plan text:

```csharp
var definition = QueryDefinitionParser.ParseFromYml(yamlText);
var planText = definition.ToYml();
var sql = context.Query(definition).ToSql();
```

`planText` is the human-readable semantic query definition, not a physical
database execution plan. Show the generated SQL and bound parameters separately
through `ToSql()` diagnostics.

## Acceptance criteria

- All 16 NorthStar metadata entities resolve uniquely with correct field types,
  physical table mappings, and relation keys.
- Each of the 20 reports has a YAML input and a readable plan-text rendering.
- Each saved request runs against the supplied SQLite seed and returns the
  same 10 result rows as its reference SQL, with equivalent values. Do not
  assume an order among rows tied on every requested sort key.
- The tests include zero-child, multi-child, null, and fan-out cases; no report
  silently overcounts or inflates values.
- User-supplied filter values remain ADO.NET parameters.
- SQL Server and PostgreSQL SQL generation remains supported for the registered
  functions. SA5 templates are fixed to one ordering and are not a general
  provider-neutral window interface.
- YAML and plan text remain the preferred authoring and review formats; the
  current Query Builder persistence contract stores saved requests as JSON.
