using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;
using NUnit.Framework.Constraints;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal class _21_QureyRequestParser : TestBase
{

    [Test]
    public void TextToFunctionFieldDto_Arguments()
    {
        var text = "Round(Id, <2>)";
        var field = (FunctionFieldDto)FieldDtoParser.Parse(text);

        var expectedField = new FunctionFieldDto("Round", [
            new FieldDto("Id"),
            new ConstFieldDto(2)
        ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    [Test]
    public void TextToFunctionFieldDto_GroupArgument()
    {
        var text = "Round(Id + <2>)";
        var field = (FunctionFieldDto)FieldDtoParser.Parse(text);

        var expectedField = new FunctionFieldDto("Round", [
            new GroupFieldDto( [
                    new FieldDto("Id"),
                    new BinaryFieldDto("+", new ConstFieldDto(2))
            ])
        ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    [Test]
    public void TextToFunctionFieldDto_Mested()
    {
        var text = "Round(Sum(Id + Age), <2>)";
        var field = (FunctionFieldDto)FieldDtoParser.Parse(text);

        var expectedField = new FunctionFieldDto("Round", [
            new FunctionFieldDto("Sum", [
                new GroupFieldDto( [
                    new FieldDto("Id"),
                    new BinaryFieldDto("+", new FieldDto("Age"))
                ])
            ]),
            new ConstFieldDto(2)
        ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    [Test]
    public void TextToFunctionFieldDto_LegacyBracketSyntax()
    {
        var field = (FunctionFieldDto)FieldDtoParser.Parse("Round[Sum[Id], <2>]");

        Assert.That(field.ToText(), Is.EqualTo("Round(Sum(Id), <2>)"));
    }

    [Test]
    public void TextToGroupFieldDto()
    {
        var text = "Id + Age";
        var field = (GroupFieldDto)FieldDtoParser.Parse(text);

        var expectedField = new GroupFieldDto([
                new FieldDto("Id"),
                new BinaryFieldDto("+", new FieldDto("Age"))
            ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    [Test]
    public void TextToGroupFieldDto_Nested()
    {
        var text = "Id + (Age * Sum(Salary))";
        var field = (GroupFieldDto)FieldDtoParser.Parse(text);

        var expectedField = new GroupFieldDto([
                new FieldDto("Id"),
                new BinaryFieldDto("+", new GroupFieldDto([
                    new FieldDto("Age"),
                    new BinaryFieldDto("*", new FunctionFieldDto("Sum", [
                        new FieldDto("Salary")
                    ]))
                ]))
            ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
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
