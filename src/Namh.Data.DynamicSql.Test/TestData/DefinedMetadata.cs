namespace Namh.Data.DynamicSql.Test.TestData;

public class DefinedMetadata
{
    public static readonly List<Entity> Metadata =
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
                //new DbField("ManagerId", "ManagerId"),
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

        new(
            Name : "Student",
            RawSql : "[User]",
            Fields : [
                new DbField("Name", "Name"),
                new DbField("Age", "Age"),
            ]
        ),
        new(
            Name : "Teacher",
            Fields : [
                new DbField("Id", "Id"),
                new DbField("Name", "Name"),
            ],
            Relations : [
                new(
                    Name : "MySchool",
                    RelatedEntity : "School",
                    Type : RelationType.Lookup,
                    RelatedKeys : [
                        new("SchoolId", "Id")
                    ])
            ]
        ),
        new(
            Name : "Calculation",
            RawSql : $"(Select t0.*, DATEDIFF(year, t0.BirthDay, GetDate()) As [CalculatedAge], t1.Count " +
                  $"From[User] t0 " +
                    $"Left Join(Select ManagerId, Count(*) Count From [User] Group By ManagerId) t1 On t1.ManagerId = t0.Id)",
            Fields : [
                new DbField("Id", "Id"),
                new DbField("Name", "Name"),
                new DbField("CalculatedAge", "CalculatedAge"),
                new DbField("Count", "Count"),
            ]
        ),

        new(
                Name: "Customers",
                Fields: [
                    new DbField("Id", "Id"),
                    new DbField("Name", "Name"),
                    new DbField("Email", "Email"),
                    new DbField("Phone", "Phone"),
                    new DbField("Address", "Address"),
                    new DbField("Segment", "Segment"),
                    new DbField("Region", "Region"),
                    new DbField("CustomerSince", "CustomerSince"),
                    new DbField("CreatedOn", "CreatedOn"),
                    new DbField("UpdatedOn", "UpdatedOn")
                ],
                Relations: [
                    new(
                        Name: "Invoices",
                        RelatedEntity: "Invoices",
                        Type: RelationType.Collection,
                        RelatedKeys: [new("Id", "CustomerId")])
                ]),
            new(
                Name: "Products",
                Fields: [
                    new DbField("Id", "Id"),
                    new DbField("Name", "Name"),
                    new DbField("Description", "Description"),
                    new DbField("Category", "Category"),
                    new DbField("UnitPrice", "UnitPrice"),
                    new DbField("CostPrice", "CostPrice"),
                    new DbField("StockQuantity", "StockQuantity"),
                    new DbField("ReorderLevel", "ReorderLevel"),
                    new DbField("CreatedOn", "CreatedOn"),
                    new DbField("UpdatedOn", "UpdatedOn")
                ],
                Relations: [
                    new(
                        Name: "InvoiceItems",
                        RelatedEntity: "InvoiceItems",
                        Type: RelationType.Collection,
                        RelatedKeys: [new("Id", "ProductId")])
                ]),
            new(
                Name: "Invoices",
                Fields: [
                    new DbField("Id", "Id"),
                    new DbField("CustomerId", "CustomerId"),
                    new DbField("InvoiceNumber", "InvoiceNumber"),
                    new DbField("InvoiceDate", "InvoiceDate"),
                    new DbField("DueDate", "DueDate"),
                    new DbField("Status", "Status"),
                    new DbField("SalesChannel", "SalesChannel"),
                    new DbField("PaymentMethod", "PaymentMethod"),
                    new DbField("PaidOn", "PaidOn"),
                    new DbField("DiscountAmount", "DiscountAmount"),
                    new DbField("TaxAmount", "TaxAmount"),
                    new DbField("TotalAmount", "TotalAmount"),
                    new DbField("CreatedOn", "CreatedOn"),
                    new DbField("UpdatedOn", "UpdatedOn")
                ],
                Relations: [
                    new(
                        Name: "Customer",
                        RelatedEntity: "Customers",
                        Type: RelationType.Lookup,
                        RelatedKeys: [new("CustomerId", "Id")]),
                    new(
                        Name: "Items",
                        RelatedEntity: "InvoiceItems",
                        Type: RelationType.Collection,
                        RelatedKeys: [new("Id", "InvoiceId")])
                ]),
            new(
                Name: "InvoiceItems",
                Fields: [
                    new DbField("Id", "Id"),
                    new DbField("InvoiceId", "InvoiceId"),
                    new DbField("ProductId", "ProductId"),
                    new DbField("Quantity", "Quantity"),
                    new DbField("UnitPrice", "UnitPrice"),
                    new DbField("DiscountAmount", "DiscountAmount"),
                    new DbField("LineTotal", "LineTotal"),
                    new DbField("CreatedOn", "CreatedOn"),
                    new DbField("UpdatedOn", "UpdatedOn")
                ],
                Relations: [
                    new(
                        Name: "Invoice",
                        RelatedEntity: "Invoices",
                        Type: RelationType.Lookup,
                        RelatedKeys: [new("InvoiceId", "Id")]),
                    new(
                        Name: "Product",
                        RelatedEntity: "Products",
                        Type: RelationType.Lookup,
                        RelatedKeys: [new("ProductId", "Id")])
                ])
    ];
}
