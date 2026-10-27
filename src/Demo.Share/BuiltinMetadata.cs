using Namh.Data.DynamicSql;

namespace Demo.Share;

public class BuiltinMetadata
{
    public static IReadOnlyCollection<Entity> SaleDemoData { get; }  =
    [
        new(
            Name: "Customers",
            Fields:
            [
                new("Id", DataType.Number), new("Name"), new("Email"), new("Phone"), new("Address"),
                new("Segment"), new("Region"), new("CustomerSince", DataType.Datetime),
                new("CreatedOn", DataType.Datetime), new("UpdatedOn", DataType.Datetime)
            ],
            Relations:
            [
                new("Invoices", "Invoices", RelationType.Collection, [new("Id", "CustomerId")])
            ]),
        new(
            Name: "Products",
            Fields:
            [
                new("Id", DataType.Number), new("Name"), new("Description"), new("Category"),
                new("UnitPrice", DataType.Number), new("CostPrice", DataType.Number),
                new("StockQuantity", DataType.Number), new("ReorderLevel", DataType.Number),
                new("CreatedOn", DataType.Datetime), new("UpdatedOn", DataType.Datetime)
            ],
            Relations:
            [
                new("InvoiceItems", "InvoiceItems", RelationType.Collection, [new("Id", "ProductId")])
            ]),
        new(
            Name: "Invoices",
            Fields:
            [
                new("Id", DataType.Number), new("CustomerId", DataType.Number), new("InvoiceNumber"),
                new("InvoiceDate", DataType.Datetime), new("DueDate", DataType.Datetime), new("Status"),
                new("SalesChannel"), new("PaymentMethod"), new("PaidOn", DataType.Datetime),
                new("DiscountAmount", DataType.Number), new("TaxAmount", DataType.Number),
                new("TotalAmount", DataType.Number), new("CreatedOn", DataType.Datetime),
                new("UpdatedOn", DataType.Datetime)
            ],
            Relations:
            [
                new("Customer", "Customers", RelationType.Lookup, [new("CustomerId", "Id")]),
                new("Items", "InvoiceItems", RelationType.Collection, [new("Id", "InvoiceId")])
            ]),
        new(
            Name: "InvoiceItems",
            Fields:
            [
                new("Id", DataType.Number), new("InvoiceId", DataType.Number), new("ProductId", DataType.Number),
                new("Quantity", DataType.Number), new("UnitPrice", DataType.Number),
                new("DiscountAmount", DataType.Number), new("LineTotal", DataType.Number),
                new("CreatedOn", DataType.Datetime), new("UpdatedOn", DataType.Datetime)
            ],
            Relations:
            [
                new("Invoice", "Invoices", RelationType.Lookup, [new("InvoiceId", "Id")]),
                new("Product", "Products", RelationType.Lookup, [new("ProductId", "Id")])
            ])
    ];

    public static IReadOnlyCollection<Entity> NorthStarDemoData { get; } =
    [
            new(
                Name: "Locations",
                Fields:
                [
                    new("Id", DataType.Number), new("Name"), new("City"), new("State")
                ],
                Relations:
                [
                    new("Employees", "Employees", RelationType.Collection, [new("Id", "LocationId")]),
                    new("Visits", "Visits", RelationType.Collection, [new("Id", "LocationId")])
                ]),
            new(
                Name: "Persons",
                Fields:
                [
                    new("Id", DataType.Number), new("FirstName"), new("LastName"),
                    new("BirthDate", DataType.Datetime), new("Gender"), new("City"), new("State")
                ],
                Relations:
                [
                    new("Orders", "Orders", RelationType.Collection, [new("Id", "PersonId")]),
                    new("PatientVisits", "Visits", RelationType.Collection, [new("Id", "PersonId")]),
                    new("MemberPolicies", "Policies", RelationType.Collection, [new("Id", "PersonId")])
                ]),
            new(
                Name: "Departments",
                Fields:
                [
                    new("Id", DataType.Number), new("Name")
                ],
                Relations:
                [
                    new("Employees", "Employees", RelationType.Collection, [new("Id", "DepartmentId")])
                ]),
            new(
                Name: "Employees",
                Fields:
                [
                    new("Id", DataType.Number), new("FirstName"), new("LastName"),
                    new("DepartmentId", DataType.Number), new("LocationId", DataType.Number),
                    new("ManagerId", DataType.Number), new("JobTitle"), new("HireDate", DataType.Datetime),
                    new("Salary", DataType.Number), new("EmploymentType")
                ],
                Relations:
                [
                    new("Department", "Departments", RelationType.Lookup, [new("DepartmentId", "Id")]),
                    new("Location", "Locations", RelationType.Lookup, [new("LocationId", "Id")]),
                    new("Manager", "Employees", RelationType.Lookup, [new("ManagerId", "Id")]),
                    new("DirectReports", "Employees", RelationType.Collection, [new("Id", "ManagerId")]),
                    new("SalesOrders", "Orders", RelationType.Collection, [new("Id", "SalesRepId")]),
                    new("PhysicianVisits", "Visits", RelationType.Collection, [new("Id", "DoctorId")]),
                    new("AgentPolicies", "Policies", RelationType.Collection, [new("Id", "AgentId")]),
                    new("PerformanceReviews", "PerformanceReviews", RelationType.Collection, [new("Id", "EmployeeId")]),
                    new("LeaveRequests", "LeaveRequests", RelationType.Collection, [new("Id", "EmployeeId")])
                ]),
            new(
                Name: "PerformanceReviews",
                Fields:
                [
                    new("Id", DataType.Number), new("EmployeeId", DataType.Number),
                    new("ReviewYear", DataType.Number), new("Rating", DataType.Number),
                    new("BonusAmount", DataType.Number)
                ],
                Relations:
                [
                    new("Employee", "Employees", RelationType.Lookup, [new("EmployeeId", "Id")])
                ]),
            new(
                Name: "LeaveRequests",
                Fields:
                [
                    new("Id", DataType.Number), new("EmployeeId", DataType.Number), new("LeaveType"),
                    new("StartDate", DataType.Datetime), new("Days", DataType.Number), new("Status")
                ],
                Relations:
                [
                    new("Employee", "Employees", RelationType.Lookup, [new("EmployeeId", "Id")])
                ]),
            // The legacy sales demo already defines a different Products entity.
            new(
                Name: "NorthStarProducts",
                Fields:
                [
                    new("Id", DataType.Number), new("Name"), new("Category"),
                    new("UnitPrice", DataType.Number), new("CostPrice", DataType.Number)
                ],
                RawSql: "Products",
                Relations:
                [
                    new("OrderItems", "OrderItems", RelationType.Collection, [new("Id", "ProductId")])
                ]),
            new(
                Name: "Orders",
                Fields:
                [
                    new("Id", DataType.Number), new("PersonId", DataType.Number),
                    new("SalesRepId", DataType.Number), new("OrderDate", DataType.Datetime),
                    new("Channel"), new("Status")
                ],
                Relations:
                [
                    new("Person", "Persons", RelationType.Lookup, [new("PersonId", "Id")]),
                    new("SalesRep", "Employees", RelationType.Lookup, [new("SalesRepId", "Id")]),
                    new("Items", "OrderItems", RelationType.Collection, [new("Id", "OrderId")])
                ]),
            new(
                Name: "OrderItems",
                Fields:
                [
                    new("Id", DataType.Number), new("OrderId", DataType.Number),
                    new("ProductId", DataType.Number), new("Quantity", DataType.Number),
                    new("UnitPrice", DataType.Number), new("DiscountAmount", DataType.Number)
                ],
                Relations:
                [
                    new("Order", "Orders", RelationType.Lookup, [new("OrderId", "Id")]),
                    new("Product", "NorthStarProducts", RelationType.Lookup, [new("ProductId", "Id")])
                ]),
            new(
                Name: "Diagnoses",
                Fields:
                [
                    new("Id", DataType.Number), new("Code"), new("Name"), new("Category"),
                    new("IsChronic", DataType.Boolean)
                ],
                Relations:
                [
                    new("Visits", "Visits", RelationType.Collection, [new("Id", "DiagnosisId")])
                ]),
            new(
                Name: "Visits",
                Fields:
                [
                    new("Id", DataType.Number), new("PersonId", DataType.Number),
                    new("DoctorId", DataType.Number), new("LocationId", DataType.Number),
                    new("VisitDate", DataType.Datetime), new("VisitType"), new("DiagnosisId", DataType.Number),
                    new("DurationMinutes", DataType.Number), new("Charge", DataType.Number), new("Status")
                ],
                Relations:
                [
                    new("Patient", "Persons", RelationType.Lookup, [new("PersonId", "Id")]),
                    new("Doctor", "Employees", RelationType.Lookup, [new("DoctorId", "Id")]),
                    new("Location", "Locations", RelationType.Lookup, [new("LocationId", "Id")]),
                    new("Diagnosis", "Diagnoses", RelationType.Lookup, [new("DiagnosisId", "Id")]),
                    new("LabResults", "LabResults", RelationType.Collection, [new("Id", "VisitId")]),
                    new("Claims", "Claims", RelationType.Collection, [new("Id", "VisitId")])
                ]),
            new(
                Name: "LabResults",
                Fields:
                [
                    new("Id", DataType.Number), new("VisitId", DataType.Number), new("TestName"),
                    new("ResultValue", DataType.Number), new("ReferenceMax", DataType.Number),
                    new("IsAbnormal", DataType.Boolean)
                ],
                Relations:
                [
                    new("Visit", "Visits", RelationType.Lookup, [new("VisitId", "Id")])
                ]),
            new(
                Name: "InsurancePlans",
                Fields:
                [
                    new("Id", DataType.Number), new("Name"), new("Tier"),
                    new("MonthlyPremium", DataType.Number), new("Deductible", DataType.Number),
                    new("OutOfPocketMax", DataType.Number), new("CoinsuranceRate", DataType.Number)
                ],
                Relations:
                [
                    new("Policies", "Policies", RelationType.Collection, [new("Id", "PlanId")])
                ]),
            new(
                Name: "Policies",
                Fields:
                [
                    new("Id", DataType.Number), new("PersonId", DataType.Number),
                    new("PlanId", DataType.Number), new("AgentId", DataType.Number),
                    new("StartDate", DataType.Datetime), new("EndDate", DataType.Datetime), new("Status")
                ],
                Relations:
                [
                    new("Person", "Persons", RelationType.Lookup, [new("PersonId", "Id")]),
                    new("Plan", "InsurancePlans", RelationType.Lookup, [new("PlanId", "Id")]),
                    new("Agent", "Employees", RelationType.Lookup, [new("AgentId", "Id")]),
                    new("PremiumPayments", "PremiumPayments", RelationType.Collection, [new("Id", "PolicyId")]),
                    new("Claims", "Claims", RelationType.Collection, [new("Id", "PolicyId")])
                ]),
            new(
                Name: "PremiumPayments",
                Fields:
                [
                    new("Id", DataType.Number), new("PolicyId", DataType.Number),
                    new("PaymentDate", DataType.Datetime), new("Amount", DataType.Number), new("Status")
                ],
                Relations:
                [
                    new("Policy", "Policies", RelationType.Lookup, [new("PolicyId", "Id")])
                ]),
            new(
                Name: "Claims",
                Fields:
                [
                    new("Id", DataType.Number), new("PolicyId", DataType.Number),
                    new("VisitId", DataType.Number), new("ClaimDate", DataType.Datetime),
                    new("BilledAmount", DataType.Number), new("AllowedAmount", DataType.Number),
                    new("PaidAmount", DataType.Number), new("Status"), new("DenialReason"),
                    new("ProcessedDate", DataType.Datetime)
                ],
                Relations:
                [
                    new("Policy", "Policies", RelationType.Lookup, [new("PolicyId", "Id")]),
                    new("Visit", "Visits", RelationType.Lookup, [new("VisitId", "Id")])
                ])
    ];

    public static List<Entity> UnitTestData { get; } =
    [
        new(
            Name: "School",
            Fields:
            [
                new("Name"),
            ],
            Relations: [
                new(
                    Name : "Students",
                    RelatedEntity : "Student",
                    Type : RelationType.Collection,
                    RelatedKeys : [
                        new("Id", "SchoolId")
                    ]
                ),
                new(
                    Name : "Teachers",
                    RelatedEntity : "Teacher",
                    Type : RelationType.Collection,
                    RelatedKeys : [
                        new("Id", "SchoolId")
                    ]
                )
            ]),

        new(
            Name : "User",
            Fields : [
                new("Id", DataType.Number),
                new("Name"),
                new("ManagerId"),
                //new("SchoolId"),
                new("Age", DataType.Number),
                new("Birthday", DataType.Datetime),
                new("IsMale", DataType.Boolean),
                //new("TotalMembers", DataType.Number, FieldType.Aggregate) { RawSql ="COUNT(*)" },
                //new("TotalAges", DataType.Number, FieldType.Aggregate) { RawSql ="SUM(@Age)" },
            ],

            Relations : [
                new Relation(
                    Name : "Manager",
                    RelatedEntity : "User",
                    Type : RelationType.Lookup,
                    RelatedKeys : [
                        new("ManagerId", "Id")
                    ]),
                new(
                    Name : "Members",
                    RelatedEntity : "User",
                    Type : RelationType.Collection,
                    RelatedKeys : [
                        new("Id", "ManagerId")
                    ]),
                new(
                    Name : "MySchool",
                    RelatedEntity : "School",
                    Type : RelationType.Lookup,
                    RelatedKeys : [
                        new("SchoolId", "Id")
                    ])
            ]),
    ];
}
