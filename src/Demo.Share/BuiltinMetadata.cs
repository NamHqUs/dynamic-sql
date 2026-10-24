using Namh.Data.DynamicSql;

namespace Demo.Share;

public class BuiltinMetadata
{
    public static IReadOnlyList<Entity> DefaultMetadata { get; } 

    static BuiltinMetadata()
    {
        DefaultMetadata = UnitTestData().Concat(SaleDemoData()).ToList();
    }
    private static IReadOnlyList<Entity> SaleDemoData() =>
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

    private static List<Entity> UnitTestData() =>
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
                //new("ManagerId"),
                //new("SchoolId"),
                new("Age", DataType.Number),
                new("Birthday", DataType.Datetime),
                new("IsMale", DataType.Boolean),
                new("TotalMembers", DataType.Number, FieldType.Aggregate) { RawSql ="Count(*)" },
                new("TotalAges", DataType.Number, FieldType.Aggregate) { RawSql ="Sum(@Age)" },
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
