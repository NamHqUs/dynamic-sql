using Namh.Data.DynamicSql;

namespace Demo.Share;

public class BuiltinMetadata
{
    public static IReadOnlyCollection<Entity> SaleDemoData { get; }  =
    [
        new(
            Name: "Customers",
            DbSchema: "sa",
            Fields:
            [
                new Field("Id", "Id"), new Field("Name", "Name"), new Field("Email", "Email"), new Field("Phone", "Phone"), new Field("Address", "Address"),
                new Field("Segment", "Segment"), new Field("Region", "Region"), new Field("CustomerSince", "CustomerSince"),
                new Field("CreatedOn", "CreatedOn"), new Field("UpdatedOn", "UpdatedOn")
            ],
            Relations:
            [
                new("Invoices", "Invoices", RelationType.Collection, [new("Id", "CustomerId")])
            ]),
        new(
            Name: "Products",
            DbSchema: "sa",
            Fields:
            [
                new Field("Id", "Id"), new Field("Name", "Name"), new Field("Description", "Description"), new Field("Category", "Category"),
                new Field("UnitPrice", "UnitPrice"), new Field("CostPrice", "CostPrice"),
                new Field("StockQuantity", "StockQuantity"), new Field("ReorderLevel", "ReorderLevel"),
                new Field("CreatedOn", "CreatedOn"), new Field("UpdatedOn", "UpdatedOn")
            ],
            Relations:
            [
                new("InvoiceItems", "InvoiceItems", RelationType.Collection, [new("Id", "ProductId")])
            ]),
        new(
            Name: "Invoices",
            DbSchema: "sa",
            Fields:
            [
                new Field("Id", "Id"), new Field("CustomerId", "CustomerId"), new Field("InvoiceNumber", "InvoiceNumber"),
                new Field("InvoiceDate", "InvoiceDate"), new Field("DueDate", "DueDate"), new Field("Status", "Status"),
                new Field("SalesChannel", "SalesChannel"), new Field("PaymentMethod", "PaymentMethod"), new Field("PaidOn", "PaidOn"),
                new Field("DiscountAmount", "DiscountAmount"), new Field("TaxAmount", "TaxAmount"),
                new Field("TotalAmount", "TotalAmount"), new Field("CreatedOn", "CreatedOn"),
                new Field("UpdatedOn", "UpdatedOn")
            ],
            Relations:
            [
                new("Customer", "Customers", RelationType.Lookup, [new("CustomerId", "Id")]),
                new("Items", "InvoiceItems", RelationType.Collection, [new("Id", "InvoiceId")])
            ]),
        new(
            Name: "InvoiceItems",
            DbSchema: "sa",
            Fields:
            [
                new Field("Id", "Id"), new Field("InvoiceId", "InvoiceId"), new Field("ProductId", "ProductId"),
                new Field("Quantity", "Quantity"), new Field("UnitPrice", "UnitPrice"),
                new Field("DiscountAmount", "DiscountAmount"), new Field("LineTotal", "LineTotal"),
                new Field("CreatedOn", "CreatedOn"), new Field("UpdatedOn", "UpdatedOn")
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
                DbSchema: "Main",
                Fields:
                [
                    new Field("Id", "Id"), new Field("Name", "Name"), new Field("City", "City"), new Field("State", "State")
                ],
                Relations:
                [
                    new("Employees", "Employees", RelationType.Collection, [new("Id", "LocationId")]),
                    new("Visits", "Visits", RelationType.Collection, [new("Id", "LocationId")])
                ]),
            new(
                Name: "Users",
                DbSchema: "Main",
                Fields:
                [
                    new Field("Id", "Id"), new Field("FirstName", "FirstName"), new Field("LastName", "LastName"),
                    new Field("BirthDate", "BirthDate"), new Field("Gender", "Gender"), new Field("City", "City"), new Field("State", "State")
                ],
                Relations:
                [
                    new("Orders", "Orders", RelationType.Collection, [new("Id", "UserId")]),
                    new("PatientVisits", "Visits", RelationType.Collection, [new("Id", "UserId")]),
                    new("MemberPolicies", "Policies", RelationType.Collection, [new("Id", "UserId")])
                ]),
            new(
                Name: "Departments",
                DbSchema: "hr",
                Fields:
                [
                    new Field("Id", "Id"), new Field("Name", "Name")
                ],
                Relations:
                [
                    new("Employees", "Employees", RelationType.Collection, [new("Id", "DepartmentId")])
                ]),
            new(
                Name: "Employees",
                DbSchema: "Main",
                Fields:
                [
                    new Field("Id", "Id"), new Field("FirstName", "FirstName"), new Field("LastName", "LastName"),
                    new Field("DepartmentId", "DepartmentId"), new Field("LocationId", "LocationId"),
                    new Field("ManagerId", "ManagerId"), new Field("JobTitle", "JobTitle"), new Field("HireDate", "HireDate"),
                    new Field("Salary", "Salary"), new Field("EmploymentType", "EmploymentType")
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
                DbSchema: "hr",
                Fields:
                [
                    new Field("Id", "Id"), new Field("EmployeeId", "EmployeeId"),
                    new Field("ReviewYear", "ReviewYear"), new Field("Rating", "Rating"),
                    new Field("BonusAmount", "BonusAmount")
                ],
                Relations:
                [
                    new("Employee", "Employees", RelationType.Lookup, [new("EmployeeId", "Id")])
                ]),
            new(
                Name: "LeaveRequests",
                DbSchema: "hr",
                Fields:
                [
                    new Field("Id", "Id"), new Field("EmployeeId", "EmployeeId"), new Field("LeaveType", "LeaveType"),
                    new Field("StartDate", "StartDate"), new Field("Days", "Days"), new Field("Status", "Status")
                ],
                Relations:
                [
                    new("Employee", "Employees", RelationType.Lookup, [new("EmployeeId", "Id")])
                ]),
            // The legacy sales demo already defines a different Products entity.
            new(
                "NorthStarProducts",
                "sa",
                "Products",
                [
                    new Field("Id", "Id"), new Field("Name", "Name"), new Field("Category", "Category"),
                    new Field("UnitPrice", "UnitPrice"), new Field("CostPrice", "CostPrice")
                ],
                [
                    new("OrderItems", "OrderItems", RelationType.Collection, [new("Id", "ProductId")])
                ]),
            new(
                Name: "Orders",
                DbSchema: "sa",
                Fields:
                [
                    new Field("Id", "Id"), new Field("UserId", "UserId"),
                    new Field("SalesRepId", "SalesRepId"), new Field("OrderDate", "OrderDate"),
                    new Field("Channel", "Channel"), new Field("Status", "Status")
                ],
                Relations:
                [
                    new("User", "Users", RelationType.Lookup, [new("UserId", "Id")]),
                    new("SalesRep", "Employees", RelationType.Lookup, [new("SalesRepId", "Id")]),
                    new("Items", "OrderItems", RelationType.Collection, [new("Id", "OrderId")])
                ]),
            new(
                Name: "OrderItems",
                DbSchema: "sa",
                Fields:
                [
                    new Field("Id", "Id"), new Field("OrderId", "OrderId"),
                    new Field("ProductId", "ProductId"), new Field("Quantity", "Quantity"),
                    new Field("UnitPrice", "UnitPrice"), new Field("DiscountAmount", "DiscountAmount")
                ],
                Relations:
                [
                    new("Order", "Orders", RelationType.Lookup, [new("OrderId", "Id")]),
                    new("Product", "NorthStarProducts", RelationType.Lookup, [new("ProductId", "Id")])
                ]),
            new(
                Name: "Diagnoses",
                DbSchema: "he",
                Fields:
                [
                    new Field("Id", "Id"), new Field("Code", "Code"), new Field("Name", "Name"), new Field("Category", "Category"),
                    new Field("IsChronic", "IsChronic")
                ],
                Relations:
                [
                    new("Visits", "Visits", RelationType.Collection, [new("Id", "DiagnosisId")])
                ]),
            new(
                Name: "Visits",
                DbSchema: "he",
                Fields:
                [
                    new Field("Id", "Id"), new Field("UserId", "UserId"),
                    new Field("DoctorId", "DoctorId"), new Field("LocationId", "LocationId"),
                    new Field("VisitDate", "VisitDate"), new Field("VisitType", "VisitType"), new Field("DiagnosisId", "DiagnosisId"),
                    new Field("DurationMinutes", "DurationMinutes"), new Field("Charge", "Charge"), new Field("Status", "Status")
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
                DbSchema: "he",
                Fields:
                [
                    new Field("Id", "Id"), new Field("VisitId", "VisitId"), new Field("TestName", "TestName"),
                    new Field("ResultValue", "ResultValue"), new Field("ReferenceMax", "ReferenceMax"),
                    new Field("IsAbnormal", "IsAbnormal")
                ],
                Relations:
                [
                    new("Visit", "Visits", RelationType.Lookup, [new("VisitId", "Id")])
                ]),
            new(
                Name: "InsurancePlans",
                DbSchema: "in",
                Fields:
                [
                    new Field("Id", "Id"), new Field("Name", "Name"), new Field("Tier", "Tier"),
                    new Field("MonthlyPremium", "MonthlyPremium"), new Field("Deductible", "Deductible"),
                    new Field("OutOfPocketMax", "OutOfPocketMax"), new Field("CoinsuranceRate", "CoinsuranceRate")
                ],
                Relations:
                [
                    new("Policies", "Policies", RelationType.Collection, [new("Id", "PlanId")])
                ]),
            new(
                Name: "Policies",
                DbSchema: "in",
                Fields:
                [
                    new Field("Id", "Id"), new Field("UserId", "UserId"),
                    new Field("PlanId", "PlanId"), new Field("AgentId", "AgentId"),
                    new Field("StartDate", "StartDate"), new Field("EndDate", "EndDate"), new Field("Status", "Status")
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
                DbSchema: "in",
                Fields:
                [
                    new Field("Id", "Id"), new Field("PolicyId", "PolicyId"),
                    new Field("PaymentDate", "PaymentDate"), new Field("Amount", "Amount"), new Field("Status", "Status")
                ],
                Relations:
                [
                    new("Policy", "Policies", RelationType.Lookup, [new("PolicyId", "Id")])
                ]),
            new(
                Name: "Claims",
                DbSchema: "in",
                Fields:
                [
                    new Field("Id", "Id"), new Field("PolicyId", "PolicyId"),
                    new Field("VisitId", "VisitId"), new Field("ClaimDate", "ClaimDate"),
                    new Field("BilledAmount", "BilledAmount"), new Field("AllowedAmount", "AllowedAmount"),
                    new Field("PaidAmount", "PaidAmount"), new Field("Status", "Status"), new Field("DenialReason", "DenialReason"),
                    new Field("ProcessedDate", "ProcessedDate")
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
                new Field("Name", "Name"),
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
                new Field("Id", "Id"),
                new Field("Name", "Name"),
                new Field("ManagerId", "ManagerId"),
                //new Field("SchoolId", "SchoolId"),
                new Field("Age", "Age"),
                new Field("Birthday", "Birthday"),
                new Field("IsMale", "IsMale"),
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
