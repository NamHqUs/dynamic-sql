# NorthStar report inputs for Dynamic SQL

## Objective

This catalog translates the 20 reports in
[northstar-demo-queries.md](./northstar-demo-queries.md) into the Dynamic SQL
query-definition language. The YAML expresses dimensions, measures, filters,
ordering, and paging; it is not a copy of the reference SQL.

The correctness target is the **same result contract** as each reference
report: the same grouping grain, measure definitions, null/zero behavior,
filters, sort order, aliases, and top 10 rows on the NorthStar seed. Different
valid SQL is expected. Prefer relationship aggregates and scalar subqueries
where they preserve the intended grain and prevent join fan-out.

## Input vocabulary

Every definition below uses the existing YAML query shape:

```yaml
Entity: SomeMetadataEntity
Projection:
  - FieldOrExpression | OutputAlias
Filter: "[FieldPath] = <value>"
Sorts: OutputAlias DESC
Page: 1
PageSize: 10
```

`Entity`, `Projection`, `Filter`, `Sorts`, `Page`, and `PageSize` are existing
query-definition concepts. Grouping is inferred from non-aggregate projections
when an aggregate is present. Field-path relationships come from
[`BuiltinMetadata.cs`](../src/Demo.Share/BuiltinMetadata.cs); for example,
`OrderItems.Product` resolves to the NorthStar `NorthStarProducts` metadata and
the physical `Products` table.

Plan text is not a second input grammar today. Parse each YAML definition into
`QueryDefinition` and render its normalized, reviewable form with
`QueryDefinition.ToYml()` / `ToString()`. Keep one source definition rather than
maintaining parallel YAML and plan-text copies. New expression nodes must
round-trip through that renderer. The catalog headings identify reports;
`YamlQueryDefinition.Name` is not currently copied into `QueryDefinition`.

The inputs also intentionally exercise reusable function extensions registered
for the NorthStar demo in
[`CustomFunctionDefintions.cs`](../src/Demo.Share/CustomFunctionDefintions.cs).
Conditional aggregate predicates are parsed into typed filter expressions and
compiled through `QueryContext`; their filter values remain parameters. These
are demo-registered custom functions, not new built-in SQL keywords.

| Expression/input | Purpose |
|------------------|---------|
| `COUNT_DISTINCT(expression)` | Exact unique counts without a duplicate-producing join |
| `COUNT_IF(predicate)` | Conditional count |
| `COUNT_DISTINCT_IF(predicate, expression)` | Conditional unique count |
| `SUM_IF(predicate, expression)` / `AVG_IF(predicate, expression)` / `MAX_IF(predicate, expression)` | Conditional measures |
| `COALESCE(value, fallback)` | Explicit result for an empty related set |
| `CONCAT(a, b, c)` | Provider-neutral display name |
| `MonthKey(date)` | Provider-neutral year-month key |
| `AgeOn(birthDate, asOfDate)` | Age at a fixed report date |
| `ElapsedYears(startDate, endDate)` | Fractional tenure duration |
| `LAG`, `Difference`, `WINDOW_SUM` | Fixed-template monthly trend functions used by SA5 |

Conditional function predicates use the same field, operator, value, and
`AND`/`OR` rules as `Filter`; the parser creates typed filter expressions,
not SQL text. The conditional aggregate functions also work as
correlated aggregates on a relationship path, such as
`PatientVisits.LabResults.CountIf(IsAbnormal = <true>)`.

`COUNT_DISTINCT`, `COALESCE`, `CONCAT`, `MonthKey`, `AgeOn`, `ElapsedYears`,
the conditional aggregates, and the SA5 functions are registered in the demo
function metadata with provider templates. Conditional aggregates also accept
predicate expressions as arguments. No example below relies on raw SQL
fragments.

For a collection relationship, an aggregate path is evaluated at that
relationship's grain. For example, a report rooted at `Persons` can project
`Orders.Items.Sum(...)` and
`Orders.CountDistinct(Id)` without joining order items into the
person-level rowset. Keep independent one-to-many measures independent; do not
join orders, payments, claims, labs, reviews, and leaves into one flat rowset
and attempt to repair inflated totals with a final `DISTINCT`.

The query model does not have a general `Windows` property or typed `OVER`
expression. SA5 uses registered custom templates instead:

```yaml
Projection:
  - LAG(SUM(Quantity * UnitPrice - DiscountAmount), <1>, MonthKey(Order.OrderDate)) | PriorMonthRevenue
  - Difference(SUM(Quantity * UnitPrice - DiscountAmount), LAG(SUM(Quantity * UnitPrice - DiscountAmount), <1>, MonthKey(Order.OrderDate))) | RevenueChange
  - WINDOW_SUM(SUM(Quantity * UnitPrice - DiscountAmount), MonthKey(Order.OrderDate)) | RunningRevenue
```

The `LAG` and running-sum templates are fixed to ascending order with no
partition clause; `Revenue` is repeated inside the expressions because the
query builder does not allow a window expression to refer to a same-select
alias. This is sufficient for the demo fixture, not a general window-query
API.

In generated SQL, these window templates evaluate after grouping and before
final sorting and paging; the query model does not represent that stage
separately. They remain registered function templates with fixed SQL
renderings, not typed window nodes.

## Saved QueryRequest JSON

The files in
[`UserData/123/QueryRequests`](../src/Demo.WebApi/FileStorage/UserData/123/QueryRequests)
are saved `QueryRequestDto` payloads, not bare `QueryDefinition` objects. Use
the persisted
[SA1](../src/Demo.WebApi/FileStorage/UserData/123/QueryRequests/SA1%20-%20Category%20and%20channel%20profitability.json)
and
[SA2](../src/Demo.WebApi/FileStorage/UserData/123/QueryRequests/SA2%20-%20Customer%20lifetime%20value%20and%20recency.json)
files as the serialization references when creating or regenerating the
twenty report definitions from [northstar-demo-queries.md](./northstar-demo-queries.md).
Do not use the adjacent `QueryRequests copy` directory as the source of truth;
its SA2 payload has different aliases, filter serialization, and designer
layout from the confirmed saved definition.

Preserve the complete request shape and designer state:

- Keep the report ID and title in `Name`, the metadata root in `Entity`, and
  include `Relations`, `FilterPlanText` (including explicit `null`), `Page`,
  `PageSize`, and `IsBuiltin`.
- Keep each projection as `{ "Fields": [...], "Alias": "..." }`. Expression
  nodes inside `Fields` use the canonical wire shape in
  [QUERY-MODEL-REFERENCE.md](../QUERY-MODEL-REFERENCE.md).
- Preserve output aliases exactly, including spaces; sort expressions that
  refer to an alias must use the same spelling. Keep `NEW_LINE` projection
  entries, encoded as an empty field with alias `NEW_LINE`, because they
  preserve designer row breaks even though execution ignores them.
- Preserve the nested `Sorts` arrays and their `Field`/`Direction` values.
- Preserve filter enum values, `parameterized` flags, and serialized literal
  types exactly as in the matching saved request. For example, SA2 stores the
  count threshold as the string `"1"`; do not silently normalize it to a JSON
  number while regenerating the payload.
- Resolve roots, relationship paths, functions, and argument order against
  [`BuiltinMetadata.cs`](../src/Demo.Share/BuiltinMetadata.cs) and
  [`CustomFunctionDefintions.cs`](../src/Demo.Share/CustomFunctionDefintions.cs).
  Do not translate SQL table aliases into metadata relationship names.

The earlier draft of SA2 was wrong because it was composed from the general
query model instead of following the saved-request examples: it changed
established aliases, dropped the `NEW_LINE` designer marker, and changed the
serialized threshold value. These changes can break alias-based sorting,
designer layout, or request compatibility even when the SQL expression looks
equivalent. Generate from the SQL result contract, then verify the persisted
shape against SA1/SA2 rather than simplifying or retyping the saved JSON.

## Sales

### SA1 — Category and channel profitability

**Result grain:** product category and order channel.
**Uses:** `COUNT_DISTINCT`.

```yaml
Entity: OrderItems
Projection:
  - Product.Category | Category
  - Order.Channel | Channel
  - COUNT_DISTINCT(Order.Id) | OrdersContainingCategory
  - SUM(Quantity) | UnitsSold
  - SUM(Quantity * UnitPrice - DiscountAmount) | NetRevenue
  - SUM(DiscountAmount) | Discounts
  - SUM(Quantity * (UnitPrice - Product.CostPrice) - DiscountAmount) | GrossProfit
  - SUM(Quantity * UnitPrice - DiscountAmount) / COUNT_DISTINCT(Order.Id) | AverageCategoryRevenuePerOrder
Filter: "[Order.Status] = <Completed>"
Sorts: GrossProfit DESC
Page: 1
PageSize: 10
```

This groups the order lines directly by category/channel. The average is
category revenue divided by the number of distinct orders containing that
category, so it does not need the reference SQL's per-order intermediate
result.

### SA2 — Customer lifetime value and recency

**Result grain:** one row per person with more than one completed order.
**Uses:** distinct count and relationship aggregate predicates.

```yaml
Entity: Persons
Projection:
  - Id | PersonId
  - CONCAT(FirstName, < >, LastName) | Customer
  - Orders.CountDistinctIf(Status = <Completed>, Id) | OrderCount
  - Orders.Items.SumIf(Order.Status = <Completed>, Quantity * UnitPrice - DiscountAmount) | LifetimeValue
  - Orders.MaxIf(Status = <Completed>, OrderDate) | LastOrderDate
  - Orders.CountDistinctIf(Status = <Completed>, Channel) | ChannelsUsed
Filter: "[Orders.CountDistinctIf(Status = <Completed>, Id)] > <1>"
Sorts: LifetimeValue DESC, LastOrderDate DESC
Page: 1
PageSize: 10
```

### SA3 — Sales representative performance

**Result grain:** one row per retail sales representative with completed sales.
**Uses:** conditional and distinct relationship aggregates; `CONCAT`.

```yaml
Entity: Employees
Projection:
  - Id | SalesRepId
  - CONCAT(FirstName, < >, LastName) | SalesRep
  - Location.City | HomeCity
  - Department.Name | Department
  - SalesOrders.CountDistinctIf(Status = <Completed>, Id) | CompletedOrders
  - SalesOrders.Items.SumIf(Order.Status = <Completed>, Quantity) | UnitsSold
  - SalesOrders.Items.SumIf(Order.Status = <Completed>, Quantity * UnitPrice - DiscountAmount) | NetSales
  - SalesOrders.Items.SumIf(Order.Status = <Completed>, Quantity * (UnitPrice - Product.CostPrice) - DiscountAmount) | GrossProfit
Filter: "[Department.Name] = <Retail Sales> AND [SalesOrders.CountIf(Status = <Completed>)] > <0>"
Sorts: NetSales DESC
Page: 1
PageSize: 10
```

### SA4 — Best-selling products and margin

**Result grain:** one row per NorthStar product.
**Uses:** conditional distinct count.

```yaml
Entity: NorthStarProducts
Projection:
  - Id | ProductId
  - Name | Product
  - Category | Category
  - OrderItems.SumIf(Order.Status = <Completed>, Quantity) | UnitsSold
  - OrderItems.CountDistinctIf(Order.Status = <Completed>, Order.Id) | OrderCount
  - OrderItems.SumIf(Order.Status = <Completed>, Quantity * UnitPrice - DiscountAmount) | NetRevenue
  - OrderItems.SumIf(Order.Status = <Completed>, Quantity * (UnitPrice - Product.CostPrice) - DiscountAmount) | GrossProfit
  - <100> * OrderItems.SumIf(Order.Status = <Completed>, Quantity * (UnitPrice - Product.CostPrice) - DiscountAmount) / NULLIF(OrderItems.SumIf(Order.Status = <Completed>, Quantity * UnitPrice - DiscountAmount), <0>) | MarginPercent
Sorts: GrossProfit DESC
Page: 1
PageSize: 10
```

### SA5 — Monthly sales trend and momentum

**Result grain:** one row per calendar month.
**Uses:** `MonthKey`, `LAG`, `Difference`, and `WINDOW_SUM` custom functions.

```yaml
Entity: OrderItems
Projection:
  - MonthKey(Order.OrderDate) | SalesMonth
  - SUM(Quantity * UnitPrice - DiscountAmount) | Revenue
  - COUNT_DISTINCT(Order.Id) | OrderCount
  - LAG(SUM(Quantity * UnitPrice - DiscountAmount), <1>, MonthKey(Order.OrderDate)) | PriorMonthRevenue
  - Difference(SUM(Quantity * UnitPrice - DiscountAmount), LAG(SUM(Quantity * UnitPrice - DiscountAmount), <1>, MonthKey(Order.OrderDate))) | RevenueChange
  - WINDOW_SUM(SUM(Quantity * UnitPrice - DiscountAmount), MonthKey(Order.OrderDate)) | RunningRevenue
Filter: "[Order.Status] = <Completed>"
Sorts: SalesMonth DESC
Page: 1
PageSize: 10
```

## Health care

### HC1 — Diagnosis burden by clinic

**Result grain:** diagnosis and clinic.
**Uses:** distinct count and conditional lab count. Lab counts are correlated
per-visit measures, preserving visit-level charge averages.

```yaml
Entity: Visits
Projection:
  - Diagnosis.Name | Diagnosis
  - Diagnosis.Category | DiagnosisCategory
  - Location.City | ClinicCity
  - COUNT() | VisitCount
  - COUNT_DISTINCT(PersonId) | UniquePatients
  - AVG(Charge) | AverageCharge
  - <100> * SUM(LabResults.CountIf(IsAbnormal = <true>)) / NULLIF(SUM(LabResults.Count()), <0>) | AbnormalLabPercent
Filter: "[Status] = <Completed>"
Sorts: VisitCount DESC, AbnormalLabPercent DESC
Page: 1
PageSize: 10
```

### HC2 — Physician workload and care-quality scorecard

**Result grain:** one row per physician, including physicians with no visits.
The employee root and correlated visit/lab measures avoid dropping empty
physicians or multiplying charges by the number of lab rows.
**Uses:** conditional and distinct related aggregates.

```yaml
Entity: Employees
Projection:
  - Id | DoctorId
  - CONCAT(FirstName, < >, LastName) | Physician
  - Department.Name | Department
  - Location.City | ClinicCity
  - PhysicianVisits.CountIf(Status = <Completed>) | VisitCount
  - PhysicianVisits.CountDistinctIf(Status = <Completed>, PersonId) | PatientCount
  - PhysicianVisits.AverageIf(Status = <Completed>, DurationMinutes) | AverageVisitMinutes
  - PhysicianVisits.SumIf(Status = <Completed>, Charge) | Charges
  - <100> * PhysicianVisits.LabResults.CountIf(Visit.Status = <Completed> AND IsAbnormal = <true>) / NULLIF(PhysicianVisits.LabResults.CountIf(Visit.Status = <Completed>), <0>) | AbnormalLabPercent
Filter: "[Department.Name] IN <Primary Care, Specialty Care, Emergency Medicine>"
Sorts: VisitCount DESC, Charges DESC
Page: 1
PageSize: 10
```

### HC3 — High-risk patient care profile

**Result grain:** one row per patient with chronic care and abnormal results.
**Uses:** conditional related aggregates and `AgeOn`.

```yaml
Entity: Persons
Projection:
  - Id | PatientId
  - CONCAT(FirstName, < >, LastName) | Patient
  - AgeOn(BirthDate, <2025-10-31>) | Age
  - PatientVisits.CountIf(Status = <Completed> AND Diagnosis.IsChronic = <true>) | ChronicVisits
  - PatientVisits.CountIf(Status = <Completed>) | CompletedVisits
  - PatientVisits.LabResults.CountIf(Visit.Status = <Completed> AND IsAbnormal = <true>) | AbnormalResults
  - PatientVisits.SumIf(Status = <Completed>, Charge) | TotalCharges
  - PatientVisits.MaxIf(Status = <Completed>, VisitDate) | LastVisitDate
Filter: "[PatientVisits.CountIf(Status = <Completed> AND Diagnosis.IsChronic = <true>)] > <0> AND [PatientVisits.LabResults.CountIf(Visit.Status = <Completed> AND IsAbnormal = <true>)] > <0>"
Sorts: AbnormalResults DESC, ChronicVisits DESC, TotalCharges DESC
Page: 1
PageSize: 10
```

### HC4 — Lab abnormality patterns

**Result grain:** lab test and diagnosis category.
This report uses current grouping and standard aggregate functions; no
conditional aggregate is required because each lab row already represents one
result.

```yaml
Entity: LabResults
Projection:
  - TestName | TestName
  - Visit.Diagnosis.Category | DiagnosisCategory
  - COUNT() | TestCount
  - SUM(IsAbnormal) | AbnormalCount
  - <100> * SUM(IsAbnormal) / COUNT() | AbnormalPercent
  - AVG(ResultValue) | AverageResult
  - AVG(ReferenceMax) | AverageReferenceMaximum
Filter: "[Visit.Status] = <Completed> AND COUNT() >= <5>"
Sorts: AbnormalPercent DESC, TestCount DESC
Page: 1
PageSize: 10
```

The aggregate comparison in `Filter` is compiled as `HAVING`; ordinary
conditions remain `WHERE`.

### HC5 — Visit-type utilization by month

**Result grain:** visit month and visit type.
**Uses:** date bucketing and conditional aggregates.

```yaml
Entity: Visits
Projection:
  - MonthKey(VisitDate) | VisitMonth
  - VisitType | VisitType
  - COUNT() | ScheduledVisits
  - COUNT_IF(Status = <No-Show>) | NoShows
  - <100> * COUNT_IF(Status = <No-Show>) / COUNT() | NoShowPercent
  - AVG_IF(Status = <Completed>, DurationMinutes) | AverageMinutes
  - SUM(Charge) | Charges
Sorts: ScheduledVisits DESC
Page: 1
PageSize: 10
```

## Insurance

### IN1 — Denial analysis by plan and reason

**Result grain:** plan and denial reason.
This report is expressible with current joins, filters, and aggregates.

```yaml
Entity: Claims
Projection:
  - Policy.Plan.Tier | Tier
  - Policy.Plan.Name | Plan
  - DenialReason | DenialReason
  - COUNT() | DeniedClaims
  - SUM(BilledAmount) | BilledAmount
  - SUM(AllowedAmount) | AllowedAmount
  - AVG(BilledAmount) | AverageBilledAmount
Filter: "[Status] = <Denied>"
Sorts: DeniedClaims DESC, BilledAmount DESC
Page: 1
PageSize: 10
```

### IN2 — Plan profitability and medical loss ratio

**Result grain:** one row per plan.
Premium and claim totals are independent aggregates over separate
relationships, so premium rows are not multiplied by claims. The displayed
ratio is illustrative, not a regulatory MLR calculation.
**Uses:** conditional related aggregates and null handling.

```yaml
Entity: InsurancePlans
Projection:
  - Name | Plan
  - Tier | Tier
  - Policies.Count() | PolicyCount
  - COALESCE(Policies.PremiumPayments.SumIf(Status = <Paid>, Amount), <0>) | PremiumRevenue
  - Policies.Claims.Count() | ClaimCount
  - COALESCE(Policies.Claims.Sum(PaidAmount), <0>) | ClaimsPaid
  - <100> * COALESCE(Policies.Claims.Sum(PaidAmount), <0>) / NULLIF(Policies.PremiumPayments.SumIf(Status = <Paid>, Amount), <0>) | MedicalLossRatioPercent
  - Policies.Claims.CountIf(Status = <Denied>) | DeniedClaims
Sorts: MedicalLossRatioPercent DESC
Page: 1
PageSize: 10
```

### IN3 — Agent portfolio and premium collection

**Result grain:** one row per insurance agent.
Nested policy/payment aggregates avoid a policy-by-payment flat join.
Only employees with at least one assigned policy are included, so an
insurance-department manager without a policy book does not consume a top-10
result slot.
**Uses:** conditional related aggregates and `CONCAT`.

```yaml
Entity: Employees
Projection:
  - Id | AgentId
  - CONCAT(FirstName, < >, LastName) | Agent
  - Location.City | OfficeCity
  - AgentPolicies.Count() | PoliciesSold
  - AgentPolicies.CountIf(Status = <Active>) | ActivePolicies
  - COALESCE(AgentPolicies.PremiumPayments.SumIf(Status = <Paid>, Amount), <0>) | PaidPremiums
  - AgentPolicies.PremiumPayments.CountIf(Status = <Late>) | LatePayments
  - AgentPolicies.PremiumPayments.CountIf(Status = <Missed>) | MissedPayments
Filter: "[Department.Name] = <Insurance Agency> AND [AgentPolicies.Count()] > <0>"
Sorts: PaidPremiums DESC
Page: 1
PageSize: 10
```

### IN4 — Claims adjudication by plan

**Result grain:** one row per plan.
**Uses:** conditional aggregates and explicit date-difference handling. `DateSub`
is already registered by the demo function definitions.

```yaml
Entity: InsurancePlans
Projection:
  - Name | Plan
  - Tier | Tier
  - Policies.Claims.Count() | ClaimsReceived
  - Policies.Claims.CountIf(Status = <Paid>) | PaidClaims
  - Policies.Claims.CountIf(Status = <Denied>) | DeniedClaims
  - <100> * Policies.Claims.CountIf(Status = <Denied>) / NULLIF(Policies.Claims.Count(), <0>) | DenialPercent
  - Policies.Claims.AverageIf(ProcessedDate IS NOT NULL, DateSub(ClaimDate, ProcessedDate)) | AverageProcessingDays
  - Policies.Claims.Sum(PaidAmount) | TotalPaid
Sorts: ClaimsReceived DESC
Page: 1
PageSize: 10
```

### IN5 — Member utilization versus premium

**Result grain:** one row per enrolled member/policy.
Policy relationships are aggregated separately for claims and premiums.
**Uses:** conditional related aggregates, `COALESCE`, and `CONCAT`.

```yaml
Entity: Policies
Projection:
  - Person.Id | MemberId
  - CONCAT(Person.FirstName, < >, Person.LastName) | Member
  - Plan.Name | Plan
  - Status | PolicyStatus
  - Claims.Count() | Claims
  - COALESCE(PremiumPayments.SumIf(Status = <Paid>, Amount), <0>) | PremiumsCollected
  - COALESCE(Claims.Sum(AllowedAmount), <0>) | AllowedAmount
  - COALESCE(Claims.Sum(PaidAmount), <0>) | InsurerPaid
  - <100> * COALESCE(Claims.Sum(PaidAmount), <0>) / NULLIF(PremiumPayments.SumIf(Status = <Paid>, Amount), <0>) | MemberLossRatioPercent
Sorts: MemberLossRatioPercent DESC, Claims DESC
Page: 1
PageSize: 10
```

## Human resources

### HR1 — Department compensation and performance

**Result grain:** one row per department. Employee payroll and review
statistics are separate relationship aggregates, preventing repeated reviews
from inflating payroll.

```yaml
Entity: Departments
Projection:
  - Name | Department
  - Employees.Count() | Headcount
  - Employees.Average(Salary) | AverageSalary
  - Employees.Sum(Salary) | AnnualPayroll
  - Employees.PerformanceReviews.AverageIf(ReviewYear = <2025>, Rating) | AverageRating
  - Employees.PerformanceReviews.SumIf(ReviewYear = <2025>, BonusAmount) | TotalBonuses
Sorts: AnnualPayroll DESC
Page: 1
PageSize: 10
```

### HR2 — Manager span and team profile

**Result grain:** one row per manager with at least one direct report.
**Uses:** conditional nested review aggregates and `CONCAT`.

```yaml
Entity: Employees
Projection:
  - Id | ManagerId
  - CONCAT(FirstName, < >, LastName) | Manager
  - Department.Name | Department
  - DirectReports.Count() | DirectReports
  - DirectReports.CountDistinct(LocationId) | TeamLocations
  - DirectReports.Average(Salary) | AverageTeamSalary
  - DirectReports.PerformanceReviews.AverageIf(ReviewYear = <2025>, Rating) | AverageTeamRating
  - DirectReports.Sum(Salary) | TeamPayroll
Filter: "[DirectReports.Count()] > <0>"
Sorts: DirectReports DESC, TeamPayroll DESC
Page: 1
PageSize: 10
```

### HR3 — Workforce composition by location

**Result grain:** one row per office.
**Uses:** conditional counts and `AgeOn`.

```yaml
Entity: Locations
Projection:
  - City | OfficeCity
  - State | State
  - Employees.Count() | Headcount
  - Employees.CountDistinct(DepartmentId) | DepartmentsRepresented
  - Employees.CountIf(EmploymentType = <Full-time>) | FullTime
  - Employees.CountIf(EmploymentType = <Part-time>) | PartTime
  - Employees.CountIf(EmploymentType = <Contract>) | Contractors
  - Employees.Average(ElapsedYears(HireDate, <2025-10-31>)) | AverageTenureYears
  - Employees.Sum(Salary) | AnnualPayroll
Sorts: Headcount DESC, AnnualPayroll DESC
Page: 1
PageSize: 10
```

### HR4 — Leave demand and approval status

**Result grain:** department, leave type, and status.
This report is expressible with current relationship joins and aggregates;
`COUNT_DISTINCT` is only needed for the unique employee measure.
**Uses:** `COUNT_DISTINCT`.

```yaml
Entity: LeaveRequests
Projection:
  - Employee.Department.Name | Department
  - LeaveType | LeaveType
  - Status | Status
  - COUNT() | Requests
  - SUM(Days) | DaysRequested
  - AVG(Days) | AverageRequestDays
  - COUNT_DISTINCT(EmployeeId) | EmployeesRequesting
Sorts: DaysRequested DESC, Requests DESC
Page: 1
PageSize: 10
```

### HR5 — Performance and bonus trends

**Result grain:** department and review year.
**Uses:** conditional count for high ratings.

```yaml
Entity: PerformanceReviews
Projection:
  - Employee.Department.Name | Department
  - ReviewYear | ReviewYear
  - COUNT() | Reviews
  - AVG(Rating) | AverageRating
  - COUNT_IF(Rating >= <4>) | HighRatings
  - <100> * COUNT_IF(Rating >= <4>) / COUNT() | HighRatingPercent
  - AVG(Employee.Salary) | AverageSalary
  - SUM(BonusAmount) | TotalBonus
Sorts: ReviewYear DESC, AverageRating DESC, TotalBonus DESC
Page: 1
PageSize: 10
```

## Implementation order

1. **Validate current-language definitions:** IN1 and HC4. Confirm related
   aggregate syntax against `QueryContext` and preserve each query's root
   grain.
2. **Add compact expression support:** distinct and conditional aggregates,
   `COALESCE`, `CONCAT`, provider-neutral `MonthKey`, `AgeOn`, and
   `ElapsedYears`. Use typed expression/predicate nodes and provider tests; do
   not add a general raw-SQL projection escape hatch.
3. **Enable all non-window reports:** verify each aggregate path lowers to a
   correlated aggregate or an ordinary grouped aggregate as intended. Extend
   the YAML model/parser only for syntax the current query model cannot express.
4. **Add windows:** add the typed `Windows` property and SQL IR for `LAG`,
   differences, and running aggregates. Apply the window layer after group
   aggregation and before sort/page.
5. **Publish YAML-first definitions:** store report definitions as YAML,
   render normalized plan text with `QueryDefinition.ToYml()`, and retain JSON
   parsing for compatibility. Add report-name metadata separately if report
   display names need to round-trip; `YamlQueryDefinition.Name` is not currently
   copied into `QueryDefinition`.

## Correctness and acceptance checks

- Compare each YAML query's output to its named reference report, by column
  alias, values, row count, and ordering. Do not require identical SQL text or
  an identical join order.
- Run the 20 inputs against `northstar-demo.sqlite.sql`; each should return
  exactly 10 rows on the included seed.
- Add edge fixtures with no related rows, multiple order items, multiple labs,
  multiple payments, and multiple reviews. Verify correlated aggregates do not
  multiply totals and that empty-set values follow the reference contract.
- Verify null handling, division by zero, date boundaries, and tied sort keys.
- Verify SQL Server, SQLite, and PostgreSQL translations for provider-neutral
  expressions; mark `DateBucket`, `AgeOn`, or window forms as provider-specific
  if an equivalent translation is not implemented.
- Ensure filter values remain ADO.NET parameters and YAML expression text is
  parsed into typed nodes rather than interpolated into SQL.
