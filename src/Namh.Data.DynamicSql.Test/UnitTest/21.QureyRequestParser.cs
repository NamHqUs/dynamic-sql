namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal class _21_QureyRequestParser : TestBase
{

    [Test]
    public void TestCases()
    {
        List<ProjectionField> fields =
        [
            new ProjectionField(new FieldDto("Name")),
            new ProjectionField(
                new FunctionFieldDto(
                    "SUM",
                    [new FieldDto("InvoiceItems.Quantity")]),
                "UnitsSold"),
            new ProjectionField(
                new FunctionFieldDto(
                    "SUM",
                    [
                        new GroupFieldDto(
                        [
                            new FieldDto("InvoiceItems.LineTotal"),
                            new BinaryFieldDto(
                                "-",
                                new GroupFieldDto(
                                [
                                    new FieldDto("InvoiceItems.Quantity"),
                                    new BinaryFieldDto(
                                        "*",
                                        new FieldDto("CostPrice"))
                                ]))
                        ])
                    ]),
                "GrossProfit"),
            new ProjectionField(
                new FunctionFieldDto(
                    "ROUND",
                    [
                        new GroupFieldDto(
                        [
                            new FunctionFieldDto(
                                "SUM",
                                [
                                    new GroupFieldDto(
                                    [
                                        new FieldDto("InvoiceItems.LineTotal"),
                                        new BinaryFieldDto(
                                            "-",
                                            new GroupFieldDto(
                                            [
                                                new FieldDto("InvoiceItems.Quantity"),
                                                new BinaryFieldDto(
                                                    "*",
                                                    new FieldDto("CostPrice"))
                                            ]))
                                    ])
                                ]),
                            new BinaryFieldDto("*", new ConstFieldDto("100.0m")),
                            new BinaryFieldDto(
                                "/",
                                new FunctionFieldDto(
                                    "NULLIF",
                                    [
                                        new FunctionFieldDto("SUM", [new FieldDto("InvoiceItems.LineTotal")]),
                                        new ConstFieldDto(0)
                                    ]))
                        ]),
                        new ConstFieldDto(2)
                    ]),
                "MarginPercent")
        ];

        var fieldsText = string.Join(", ", fields.Select(field => field.ToText()));
        var parsedFields = ProjectionField.FromTextList(fieldsText);
        Assert.That(parsedFields.Select(field => field.ToText()), Is.EqualTo(fields.Select(field => field.ToText())));

        foreach (var field in fields)
        {
            var text = field.ToText();
            var field2 = ProjectionField.FromText(text);
            var text2 = field2.ToText();
            AssertEquivalent(field.Field, field2.Field);
            Assert.That(field2.Alias, Is.EqualTo(field.Alias));
            Assert.That(text, Is.EqualTo(text2));
        }
    }

    private static void AssertEquivalent(IFieldDto expected, IFieldDto actual)
    {
        switch (expected, actual)
        {
            case (FieldDto expectedField, FieldDto actualField):
                Assert.That(actualField.FieldName, Is.EqualTo(expectedField.FieldName));
                break;
            case (ConstFieldDto expectedConstraint, ConstFieldDto actualConstraint):
                Assert.That(actualConstraint.Value, Is.EqualTo(expectedConstraint.Value));
                break;
            case (BinaryFieldDto expectedBinary, BinaryFieldDto actualBinary):
                Assert.That(actualBinary.Oper, Is.EqualTo(expectedBinary.Oper));
                AssertEquivalent(expectedBinary.Field, actualBinary.Field);
                break;
            case (FunctionFieldDto expectedFunction, FunctionFieldDto actualFunction):
                Assert.That(actualFunction.FunctionName, Is.EqualTo(expectedFunction.FunctionName));
                Assert.That(actualFunction.Arguments, Has.Length.EqualTo(expectedFunction.Arguments.Length));
                for (var i = 0; i < expectedFunction.Arguments.Length; i++)
                {
                    AssertEquivalent(expectedFunction.Arguments[i], actualFunction.Arguments[i]);
                }
                break;
            case (GroupFieldDto expectedGroup, GroupFieldDto actualGroup):
                Assert.That(actualGroup.Fields, Has.Length.EqualTo(expectedGroup.Fields.Length));
                for (var i = 0; i < expectedGroup.Fields.Length; i++)
                {
                    AssertEquivalent(expectedGroup.Fields[i], actualGroup.Fields[i]);
                }
                break;
            default:
                Assert.Fail($"Expected {expected.GetType().Name}, got {actual.GetType().Name}.");
                break;
        }
    }
}
