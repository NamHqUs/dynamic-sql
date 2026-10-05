namespace Namh.Data.DynamicSql.ConsoleTest.TestData;

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
                new("ManagerId"),
                new("SchoolId"),
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
        )
    ];
}
