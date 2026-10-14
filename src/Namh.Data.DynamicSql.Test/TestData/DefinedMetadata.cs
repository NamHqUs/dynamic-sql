namespace Namh.Data.DynamicSql.Test.TestData;

public class DefinedMetadata
{
    public static readonly List<Entity> Metadata =
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
                new("Id"),
                new("Name"),
                //new("ManagerId"),
                //new("SchoolId"),
                new("Age"),
                new("Birthday"),
                new("IsMale"),
                new("DeletedOn"),
                new("TotalMembers", FieldType: FieldType.Aggregate) { RawSql ="Count(*)" },
                new("TotalAges", FieldType: FieldType.Aggregate) { RawSql ="Sum(@Age)" },
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
                new("Name"),
                new("Age"),
                new("DeletedOn"),
                new("TotalMembers", FieldType: FieldType.Aggregate) { RawSql ="Count(*)" },
                new("TotalAges",  FieldType: FieldType.Aggregate) { RawSql ="Sum(Age)" },
                new("CountAgeWithNam", FieldType: FieldType.Aggregate) { RawSql ="Count(*)$Name='Nam'" }
            ]
        ),
        new(
            Name : "Teacher",
            Fields : [
                new("Id"),
                new("Name"),
                new("DeletedOn"),
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
                new("Id"),
                new("Name"),
                new("CalculatedAge"),
                new("Count"),
            ]
        ),

        new(
                Name: "Customers",
                Fields: [
                    new("Id", DataType.Number),
                    new("Name"),
                    new("Email"),
                    new("Phone"),
                    new("Address"),
                    new("Segment"),
                    new("Region"),
                    new("CustomerSince", DataType.Datetime),
                    new("CreatedOn", DataType.Datetime),
                    new("UpdatedOn", DataType.Datetime),
                    new("DeletedOn", DataType.Datetime)
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
                    new("Id", DataType.Number),
                    new("Name"),
                    new("Description"),
                    new("Category"),
                    new("UnitPrice", DataType.Number),
                    new("CostPrice", DataType.Number),
                    new("StockQuantity", DataType.Number),
                    new("ReorderLevel", DataType.Number),
                    new("CreatedOn", DataType.Datetime),
                    new("UpdatedOn", DataType.Datetime),
                    new("DeletedOn", DataType.Datetime)
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
                    new("Id", DataType.Number),
                    new("CustomerId", DataType.Number),
                    new("InvoiceNumber"),
                    new("InvoiceDate", DataType.Datetime),
                    new("DueDate", DataType.Datetime),
                    new("Status"),
                    new("SalesChannel"),
                    new("PaymentMethod"),
                    new("PaidOn", DataType.Datetime),
                    new("DiscountAmount", DataType.Number),
                    new("TaxAmount", DataType.Number),
                    new("TotalAmount", DataType.Number),
                    new("CreatedOn", DataType.Datetime),
                    new("UpdatedOn", DataType.Datetime),
                    new("DeletedOn", DataType.Datetime)
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
                    new("Id", DataType.Number),
                    new("InvoiceId", DataType.Number),
                    new("ProductId", DataType.Number),
                    new("Quantity", DataType.Number),
                    new("UnitPrice", DataType.Number),
                    new("DiscountAmount", DataType.Number),
                    new("LineTotal", DataType.Number),
                    new("CreatedOn", DataType.Datetime),
                    new("UpdatedOn", DataType.Datetime),
                    new("DeletedOn", DataType.Datetime)
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
