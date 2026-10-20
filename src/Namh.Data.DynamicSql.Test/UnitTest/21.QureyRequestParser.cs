using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal class _21_QureyRequestParser : TestBase
{

    [Test]
    public void TextToFunctionFieldDto_Arguments()
    {
        var text = "Round(Id, <2>)";
        var field = (FunctionFieldDefinition)QueryDefinitionParser.ParseField(text);

        var expectedField = new FunctionFieldDefinition("Round", [
            new FieldDefinition("Id"),
            new ConstFieldDefinition(2)
        ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    [Test]
    public void TextToFunctionFieldDto_GroupArgument()
    {
        var text = "Round(Id + <2>)";
        var field = (FunctionFieldDefinition)QueryDefinitionParser.ParseField(text);

        var expectedField = new FunctionFieldDefinition("Round", [
            new GroupFieldDefintion( [
                    new FieldDefinition("Id"),
                    new BinaryFieldDefinition("+", new ConstFieldDefinition(2))
            ])
        ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    [Test]
    public void TextToFunctionFieldDto_Mested()
    {
        var text = "Round(Sum(Id + Age), <2>)";
        var field = (FunctionFieldDefinition)QueryDefinitionParser.ParseField(text);

        var expectedField = new FunctionFieldDefinition("Round", [
            new FunctionFieldDefinition("Sum", [
                new GroupFieldDefintion( [
                    new FieldDefinition("Id"),
                    new BinaryFieldDefinition("+", new FieldDefinition("Age"))
                ])
            ]),
            new ConstFieldDefinition(2)
        ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    [Test]
    public void TextToFunctionFieldDto_LegacyBracketSyntax()
    {
        var field = (FunctionFieldDefinition)QueryDefinitionParser.ParseProjection("Round[Sum[Id], <2>]")[0].Field;

        Assert.That(field.ToText(), Is.EqualTo("Round(Sum(Id), <2>)"));
    }

    [Test]
    public void TextToGroupFieldDto()
    {
        var text = "Id + Age";
        var field = (GroupFieldDefintion)QueryDefinitionParser.ParseField(text);

        var expectedField = new GroupFieldDefintion([
                new FieldDefinition("Id"),
                new BinaryFieldDefinition("+", new FieldDefinition("Age"))
            ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    [Test]
    public void TextToGroupFieldDto_Nested()
    {
        var text = "Id + (Age * Sum(Salary))";
        var field = (GroupFieldDefintion)QueryDefinitionParser.ParseField(text);

        var expectedField = new GroupFieldDefintion([
                new FieldDefinition("Id"),
                new BinaryFieldDefinition("+", new GroupFieldDefintion([
                    new FieldDefinition("Age"),
                    new BinaryFieldDefinition("*", new FunctionFieldDefinition("Sum", [
                        new FieldDefinition("Salary")
                    ]))
                ]))
            ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    private static void AssertEquivalent(IFieldDefinition expected, IFieldDefinition actual)
    {
        switch (expected, actual)
        {
            case (FieldDefinition expectedField, FieldDefinition actualField):
                Assert.That(actualField.FieldName, Is.EqualTo(expectedField.FieldName));
                break;
            case (ConstFieldDefinition expectedConstraint, ConstFieldDefinition actualConstraint):
                Assert.That(actualConstraint.Value, Is.EqualTo(expectedConstraint.Value));
                break;
            case (BinaryFieldDefinition expectedBinary, BinaryFieldDefinition actualBinary):
                Assert.That(actualBinary.Oper, Is.EqualTo(expectedBinary.Oper));
                AssertEquivalent(expectedBinary.Field, actualBinary.Field);
                break;
            case (FunctionFieldDefinition expectedFunction, FunctionFieldDefinition actualFunction):
                Assert.That(actualFunction.FunctionName, Is.EqualTo(expectedFunction.FunctionName));
                Assert.That(actualFunction.Arguments, Has.Length.EqualTo(expectedFunction.Arguments.Length));
                for (var i = 0; i < expectedFunction.Arguments.Length; i++)
                {
                    AssertEquivalent(expectedFunction.Arguments[i], actualFunction.Arguments[i]);
                }
                break;
            case (GroupFieldDefintion expectedGroup, GroupFieldDefintion actualGroup):
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
