using Microsoft.EntityFrameworkCore.Query.Internal;
using Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    [TestCase("A - B * (C - D) / E + <3>", "A - (B * (C - D) / E) + <3>")]
    public void SubGroups(string input, string expected)
    {
        var group = (GroupFieldDefintion)QueryDefinitionParser.ParseField(input);

        var text = JsonSerializer.Serialize(group,
            new JsonSerializerOptions { WriteIndented = true }
        );
        Assert.That(group.ToText().Replace(" ", ""), Is.EqualTo(expected.Replace(" ", "")));
    }

    [Test]
    public void SubGroups1()
    {
        var group = (GroupFieldDefintion)QueryDefinitionParser.ParseField("A - B * (C - D) / E + <3>");

        Assert.That(group.Oper, Is.EqualTo(MatchOperation.Add));
        Assert.That(group.Fields, Has.Length.EqualTo(3));
        Assert.That(group.Fields[0], Is.EqualTo(new FieldDefinition("A")));

        var subtractedGroup = (GroupFieldDefintion)group.Fields[1];
        Assert.That(subtractedGroup.Oper, Is.EqualTo(MatchOperation.Subtract));
        Assert.That(subtractedGroup.Fields, Has.Length.EqualTo(3));
        Assert.That(subtractedGroup.Fields[0], Is.EqualTo(new FieldDefinition("B")));
        Assert.That(subtractedGroup.Fields[2], Is.EqualTo(new MathFieldDefinition(
            MatchOperation.Divide,
            new FieldDefinition("E"))));

        var multiplicativeGroup = (GroupFieldDefintion)subtractedGroup.Fields[1];
        Assert.That(multiplicativeGroup.Oper, Is.EqualTo(MatchOperation.Multiply));
        Assert.That(multiplicativeGroup.Fields, Has.Length.EqualTo(2));
        Assert.That(multiplicativeGroup.Fields[0], Is.EqualTo(new FieldDefinition("C")));
        Assert.That(multiplicativeGroup.Fields[1], Is.EqualTo(new MathFieldDefinition(
            MatchOperation.Subtract,
            new FieldDefinition("D"))));
        Assert.That(group.Fields[2], Is.EqualTo(new MathFieldDefinition(
            MatchOperation.Add,
            new ConstFieldDefinition(3))));
    }

    [Test]
    public void SubGroups_JsonSerialize()
    {
        var group = (GroupFieldDefintion)QueryDefinitionParser.ParseField("A - B * (C - D) / E + <3>");

        var text = JsonSerializer.Serialize<IFieldDefinition>(group, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });

        var expectedText = """
            {
              "oper": "Add",
              "kind": "group",
              "fields": [
                {
                  "kind": "field",
                  "field": "A"
                },
                {
                  "kind": "group",
                  "operator": "-",
                  "arguments": [
                    {
                      "kind": "field",
                      "field": "B"
                    },
                    {
                      "kind": "group",
                      "operator": "*",
                      "arguments": [
                        {
                          "kind": "field",
                          "field": "C"
                        },
                        {
                          "oper": "Subtract",
                          "kind": "math",
                          "field": {
                            "kind": "field",
                            "field": "D"
                          }
                        }
                      ]
                    },
                    {
                      "oper": "Divide",
                      "kind": "math",
                      "field": {
                        "kind": "field",
                        "field": "E"
                      }
                    }
                  ]
                },
                {
                  "oper": "Add",
                  "kind": "math",
                  "field": {
                    "kind": "value",
                    "value": 3
                  }
                }
              ]
            }
            """;

        Assert.That(text.Replace(" ", "").Replace("\r", "").Replace("\n", ""), Is.EqualTo(expectedText.Replace(" ", "").Replace("\r", "").Replace("\n", "")));

        var roundTripped = JsonSerializer.Deserialize<IFieldDefinition>(text, new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        }) ?? throw new InvalidOperationException("The serialized field expression did not produce a definition.");
        Assert.That(roundTripped.ToText(), Is.EqualTo(group.ToText()));
    }

    [Test]
    public void SubGroups_Operator_Priority_JsonSerialize()
    {
        var group = (GroupFieldDefintion)QueryDefinitionParser.ParseField("A - B * C + <3>");

        var text = JsonSerializer.Serialize<IFieldDefinition>(group, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });

        var expectedText = """
            {
              "oper": "Add",
              "kind": "group",
              "fields": [
                {
                  "kind": "field",
                  "field": "A"
                },
                {
                  "kind": "group",
                  "operator": "-",
                  "arguments": [
                    {
                      "kind": "field",
                      "field": "B"
                    },
                    {
                      "kind": "group",
                      "operator": "*",
                      "arguments": [
                        {
                          "kind": "field",
                          "field": "C"
                        },
                        {
                          "oper": "Subtract",
                          "kind": "math",
                          "field": {
                            "kind": "field",
                            "field": "D"
                          }
                        }
                      ]
                    },
                    {
                      "oper": "Divide",
                      "kind": "math",
                      "field": {
                        "kind": "field",
                        "field": "E"
                      }
                    }
                  ]
                },
                {
                  "oper": "Add",
                  "kind": "math",
                  "field": {
                    "kind": "value",
                    "value": 3
                  }
                }
              ]
            }
            """;

        Assert.That(text.Replace(" ", "").Replace("\r", "").Replace("\n", ""), Is.EqualTo(expectedText.Replace(" ", "").Replace("\r", "").Replace("\n", "")));

        var roundTripped = JsonSerializer.Deserialize<IFieldDefinition>(text, new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        }) ?? throw new InvalidOperationException("The serialized field expression did not produce a definition.");
        Assert.That(roundTripped.ToText(), Is.EqualTo(group.ToText()));
    }

    [Test]
    public void SubGroups_JsonDeserialize()
    {
        var json = """
            {
              "oper": "Add",
              "kind": "group",
              "fields": [
                {
                  "kind": "field",
                  "field": "A"
                },
                {
                  "kind": "group",
                  "operator": "-",
                  "arguments": [
                    {
                      "kind": "field",
                      "field": "B"
                    },
                    {
                      "kind": "group",
                      "operator": "*",
                      "arguments": [
                        {
                          "kind": "field",
                          "field": "C"
                        },
                        {
                          "oper": "Subtract",
                          "kind": "math",
                          "field": {
                            "kind": "field",
                            "field": "D"
                          }
                        }
                      ]
                    },
                    {
                      "oper": "Divide",
                      "kind": "math",
                      "field": {
                        "kind": "field",
                        "field": "E"
                      }
                    }
                  ]
                },
                {
                  "oper": "Add",
                  "kind": "math",
                  "field": {
                    "kind": "value",
                    "value": 3
                  }
                }
              ]
            }
            """;

        var group = (GroupFieldDefintion) JsonSerializer.Deserialize<IFieldDefinition>(json, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        })!;

        Assert.That(group.Oper, Is.EqualTo(MatchOperation.Add));
        Assert.That(group.Fields, Has.Length.EqualTo(3));
        Assert.That(group.Fields[0], Is.EqualTo(new FieldDefinition("A")));

        var subtractedGroup = (GroupFieldDefintion)group.Fields[1];
        Assert.That(subtractedGroup.Oper, Is.EqualTo(MatchOperation.Subtract));
        Assert.That(subtractedGroup.Fields, Has.Length.EqualTo(3));
        Assert.That(subtractedGroup.Fields[0], Is.EqualTo(new FieldDefinition("B")));
        Assert.That(subtractedGroup.Fields[2], Is.EqualTo(new MathFieldDefinition(
            MatchOperation.Divide,
            new FieldDefinition("E"))));

        var multiplicativeGroup = (GroupFieldDefintion)subtractedGroup.Fields[1];
        Assert.That(multiplicativeGroup.Oper, Is.EqualTo(MatchOperation.Multiply));
        Assert.That(multiplicativeGroup.Fields, Has.Length.EqualTo(2));
        Assert.That(multiplicativeGroup.Fields[0], Is.EqualTo(new FieldDefinition("C")));
        Assert.That(multiplicativeGroup.Fields[1], Is.EqualTo(new MathFieldDefinition(
            MatchOperation.Subtract,
            new FieldDefinition("D"))));
        Assert.That(group.Fields[2], Is.EqualTo(new MathFieldDefinition(
            MatchOperation.Add,
            new ConstFieldDefinition(3L))));

    }

    [Test]
    public void FieldDefinition_JsonParser()
    {
        var expression = "A + (Rount(B, 2) * (B - 1) - C) + <4>";
        var group = (GroupFieldDefintion)QueryDefinitionParser.ParseField(expression);

        var text = JsonSerializer.Serialize<IFieldDefinition>(group, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
    }
}
