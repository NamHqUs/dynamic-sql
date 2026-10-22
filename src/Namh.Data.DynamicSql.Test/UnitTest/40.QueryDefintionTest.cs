using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal class _40_QueryDefintionTest : TestBase
{
    [Test]
    public void AdditiveExpressionUsesOneFlatGroup()
    {
        const string text = "A + B + C - D";

        var group = (GroupFieldDefintion)QueryDefinitionParser.ParseField(text);

        Assert.That(group.Oper, Is.EqualTo(MatchOperation.Add));
        Assert.That(group.Fields, Has.Length.EqualTo(4));
        Assert.That(group.Fields[0], Is.EqualTo(new FieldDefinition("A")));
        Assert.That(group.Fields[1], Is.EqualTo(new MathFieldDefinition(
            MatchOperation.Add,
            new FieldDefinition("B"))));
        Assert.That(group.Fields[2], Is.EqualTo(new MathFieldDefinition(
            MatchOperation.Add,
            new FieldDefinition("C"))));
        Assert.That(group.Fields[3], Is.EqualTo(new MathFieldDefinition(
            MatchOperation.Subtract,
            new FieldDefinition("D"))));
        Assert.That(group.ToText(), Is.EqualTo(text));
    }

    [Test]
    public void MultiplicativeTermsRemainNestedInsideAdditiveGroup()
    {
        var group = (GroupFieldDefintion)QueryDefinitionParser.ParseField("A + B * C - D");

        Assert.That(group.Fields, Has.Length.EqualTo(3));
        var multiplicativeGroup = (GroupFieldDefintion)group.Fields[1];
        Assert.That(multiplicativeGroup.Oper, Is.EqualTo(MatchOperation.Add));
        Assert.That(multiplicativeGroup.Fields[0], Is.EqualTo(new FieldDefinition("B")));
        Assert.That(multiplicativeGroup.Fields[1], Is.EqualTo(new MathFieldDefinition(
            MatchOperation.Multiply,
            new FieldDefinition("C"))));
        Assert.That(group.Fields[1], Is.Not.InstanceOf<MathFieldDefinition>());
        Assert.That(group.Fields[2], Is.EqualTo(new MathFieldDefinition(
            MatchOperation.Subtract,
            new FieldDefinition("D"))));
    }

    [TestCase("A + B - C", "A + B - C")]
    [TestCase("A * B / C", "A * B / C")]
    [TestCase("A + (B - C)", "A + (B - C)")]
    [TestCase("A + B * C - D", "A + (B * C) - D")]
    [TestCase("A - B * (C - D)/E + 3", "A - (B * (C - D) / E) + 3")]
    public void SubGroups(string input, string expected)
    {
        var group = (GroupFieldDefintion)QueryDefinitionParser.ParseField(input);

        Assert.That(group.ToText().Replace(" ", ""), Is.EqualTo(expected.Replace(" ", "")));
    }
}
