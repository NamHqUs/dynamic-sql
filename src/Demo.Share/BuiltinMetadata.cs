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
                new DbField("Id", "Id"), new DbField("Name", "Name"), new DbField("Email", "Email"), new DbField("Phone", "Phone"), new DbField("Address", "Address"),
                new DbField("Segment", "Segment"), new DbField("Region", "Region"), new DbField("CustomerSince", "CustomerSince"),
                new DbField("CreatedOn", "CreatedOn"), new DbField("UpdatedOn", "UpdatedOn")
            ],
            Relations:
            [
                new("Invoices", "Invoices", RelationType.Collection, [new("Id", "CustomerId")])
            ]),
        new(
            Name: "Products",
            Fields:
            [
                new DbField("Id", "Id"), new DbField("Name", "Name"), new DbField("Description", "Description"), new DbField("Category", "Category"),
                new DbField("UnitPrice", "UnitPrice"), new DbField("CostPrice", "CostPrice"),
                new DbField("StockQuantity", "StockQuantity"), new DbField("ReorderLevel", "ReorderLevel"),
                new DbField("CreatedOn", "CreatedOn"), new DbField("UpdatedOn", "UpdatedOn")
            ],
            Relations:
            [
                new("InvoiceItems", "InvoiceItems", RelationType.Collection, [new("Id", "ProductId")])
            ]),
        new(
            Name: "Invoices",
            Fields:
            [
                new DbField("Id", "Id"), new DbField("CustomerId", "CustomerId"), new DbField("InvoiceNumber", "InvoiceNumber"),
                new DbField("InvoiceDate", "InvoiceDate"), new DbField("DueDate", "DueDate"), new DbField("Status", "Status"),
                new DbField("SalesChannel", "SalesChannel"), new DbField("PaymentMethod", "PaymentMethod"), new DbField("PaidOn", "PaidOn"),
                new DbField("DiscountAmount", "DiscountAmount"), new DbField("TaxAmount", "TaxAmount"),
                new DbField("TotalAmount", "TotalAmount"), new DbField("CreatedOn", "CreatedOn"),
                new DbField("UpdatedOn", "UpdatedOn")
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
                new DbField("Id", "Id"), new DbField("InvoiceId", "InvoiceId"), new DbField("ProductId", "ProductId"),
                new DbField("Quantity", "Quantity"), new DbField("UnitPrice", "UnitPrice"),
                new DbField("DiscountAmount", "DiscountAmount"), new DbField("LineTotal", "LineTotal"),
                new DbField("CreatedOn", "CreatedOn"), new DbField("UpdatedOn", "UpdatedOn")
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
                    new DbField("Id", "Id"), new DbField("Name", "Name"), new DbField("City", "City"), new DbField("State", "State")
                ],
                Relations:
                [
                    new("Employees", "Employees", RelationType.Collection, [new("Id", "LocationId")]),
                    new("Visits", "Visits", RelationType.Collection, [new("Id", "LocationId")])
                ]),
            new(
                Name: "Users",
                Fields:
                [
                    new DbField("Id", "Id"), new DbField("FirstName", "FirstName"), new DbField("LastName", "LastName"),
                    new DbField("BirthDate", "BirthDate"), new DbField("Gender", "Gender"), new DbField("City", "City"), new DbField("State", "State")
                ],
                Relations:
                [
                    new("Orders", "Orders", RelationType.Collection, [new("Id", "UserId")]),
                    new("PatientVisits", "Visits", RelationType.Collection, [new("Id", "UserId")]),
                    new("MemberPolicies", "Policies", RelationType.Collection, [new("Id", "UserId")])
                ]),
            new(
                Name: "Departments",
                Fields:
                [
                    new DbField("Id", "Id"), new DbField("Name", "Name")
                ],
                Relations:
                [
                    new("Employees", "Employees", RelationType.Collection, [new("Id", "DepartmentId")])
                ]),
            new(
                Name: "Employees",
                Fields:
                [
                    new DbField("Id", "Id"), new DbField("FirstName", "FirstName"), new DbField("LastName", "LastName"),
                    new DbField("DepartmentId", "DepartmentId"), new DbField("LocationId", "LocationId"),
                    new DbField("ManagerId", "ManagerId"), new DbField("JobTitle", "JobTitle"), new DbField("HireDate", "HireDate"),
                    new DbField("Salary", "Salary"), new DbField("EmploymentType", "EmploymentType")
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
                    new DbField("Id", "Id"), new DbField("EmployeeId", "EmployeeId"),
                    new DbField("ReviewYear", "ReviewYear"), new DbField("Rating", "Rating"),
                    new DbField("BonusAmount", "BonusAmount")
                ],
                Relations:
                [
                    new("Employee", "Employees", RelationType.Lookup, [new("EmployeeId", "Id")])
                ]),
            new(
                Name: "LeaveRequests",
                Fields:
                [
                    new DbField("Id", "Id"), new DbField("EmployeeId", "EmployeeId"), new DbField("LeaveType", "LeaveType"),
                    new DbField("StartDate", "StartDate"), new DbField("Days", "Days"), new DbField("Status", "Status")
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
                    new DbField("Id", "Id"), new DbField("Name", "Name"), new DbField("Category", "Category"),
                    new DbField("UnitPrice", "UnitPrice"), new DbField("CostPrice", "CostPrice")
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
                    new DbField("Id", "Id"), new DbField("UserId", "UserId"),
                    new DbField("SalesRepId", "SalesRepId"), new DbField("OrderDate", "OrderDate"),
                    new DbField("Channel", "Channel"), new DbField("Status", "Status")
                ],
                Relations:
                [
                    new("User", "Users", RelationType.Lookup, [new("UserId", "Id")]),
                    new("SalesRep", "Employees", RelationType.Lookup, [new("SalesRepId", "Id")]),
                    new("Items", "OrderItems", RelationType.Collection, [new("Id", "OrderId")])
                ]),
            new(
                Name: "OrderItems",
                Fields:
                [
                    new DbField("Id", "Id"), new DbField("OrderId", "OrderId"),
                    new DbField("ProductId", "ProductId"), new DbField("Quantity", "Quantity"),
                    new DbField("UnitPrice", "UnitPrice"), new DbField("DiscountAmount", "DiscountAmount")
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
                    new DbField("Id", "Id"), new DbField("Code", "Code"), new DbField("Name", "Name"), new DbField("Category", "Category"),
                    new DbField("IsChronic", "IsChronic")
                ],
                Relations:
                [
                    new("Visits", "Visits", RelationType.Collection, [new("Id", "DiagnosisId")])
                ]),
            new(
                Name: "Visits",
                Fields:
                [
                    new DbField("Id", "Id"), new DbField("UserId", "UserId"),
                    new DbField("DoctorId", "DoctorId"), new DbField("LocationId", "LocationId"),
                    new DbField("VisitDate", "VisitDate"), new DbField("VisitType", "VisitType"), new DbField("DiagnosisId", "DiagnosisId"),
                    new DbField("DurationMinutes", "DurationMinutes"), new DbField("Charge", "Charge"), new DbField("Status", "Status")
                ],
                Relations:
                [
                    new("Patient", "Users", RelationType.Lookup, [new("UserId", "Id")]),
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
                    new DbField("Id", "Id"), new DbField("VisitId", "VisitId"), new DbField("TestName", "TestName"),
                    new DbField("ResultValue", "ResultValue"), new DbField("ReferenceMax", "ReferenceMax"),
                    new DbField("IsAbnormal", "IsAbnormal")
                ],
                Relations:
                [
                    new("Visit", "Visits", RelationType.Lookup, [new("VisitId", "Id")])
                ]),
            new(
                Name: "InsurancePlans",
                Fields:
                [
                    new DbField("Id", "Id"), new DbField("Name", "Name"), new DbField("Tier", "Tier"),
                    new DbField("MonthlyPremium", "MonthlyPremium"), new DbField("Deductible", "Deductible"),
                    new DbField("OutOfPocketMax", "OutOfPocketMax"), new DbField("CoinsuranceRate", "CoinsuranceRate")
                ],
                Relations:
                [
                    new("Policies", "Policies", RelationType.Collection, [new("Id", "PlanId")])
                ]),
            new(
                Name: "Policies",
                Fields:
                [
                    new DbField("Id", "Id"), new DbField("UserId", "UserId"),
                    new DbField("PlanId", "PlanId"), new DbField("AgentId", "AgentId"),
                    new DbField("StartDate", "StartDate"), new DbField("EndDate", "EndDate"), new DbField("Status", "Status")
                ],
                Relations:
                [
                    new("User", "Users", RelationType.Lookup, [new("UserId", "Id")]),
                    new("Plan", "InsurancePlans", RelationType.Lookup, [new("PlanId", "Id")]),
                    new("Agent", "Employees", RelationType.Lookup, [new("AgentId", "Id")]),
                    new("PremiumPayments", "PremiumPayments", RelationType.Collection, [new("Id", "PolicyId")]),
                    new("Claims", "Claims", RelationType.Collection, [new("Id", "PolicyId")])
                ]),
            new(
                Name: "PremiumPayments",
                Fields:
                [
                    new DbField("Id", "Id"), new DbField("PolicyId", "PolicyId"),
                    new DbField("PaymentDate", "PaymentDate"), new DbField("Amount", "Amount"), new DbField("Status", "Status")
                ],
                Relations:
                [
                    new("Policy", "Policies", RelationType.Lookup, [new("PolicyId", "Id")])
                ]),
            new(
                Name: "Claims",
                Fields:
                [
                    new DbField("Id", "Id"), new DbField("PolicyId", "PolicyId"),
                    new DbField("VisitId", "VisitId"), new DbField("ClaimDate", "ClaimDate"),
                    new DbField("BilledAmount", "BilledAmount"), new DbField("AllowedAmount", "AllowedAmount"),
                    new DbField("PaidAmount", "PaidAmount"), new DbField("Status", "Status"), new DbField("DenialReason", "DenialReason"),
                    new DbField("ProcessedDate", "ProcessedDate")
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
                new DbField("Name", "Name"),
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
                new DbField("Id", "Id"),
                new DbField("Name", "Name"),
                new DbField("ManagerId", "ManagerId"),
                //new DbField("SchoolId", "SchoolId"),
                new DbField("Age", "Age"),
                new DbField("Birthday", "Birthday"),
                new DbField("IsMale", "IsMale"),
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
