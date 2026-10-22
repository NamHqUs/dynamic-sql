# Expression Model JSON Reference

This document records the canonical JSON shape used when serializing backend
projection expressions. It is distinct from the frontend's TypeScript
`IFieldDefinition` type; do not infer the wire format from that type alone.

The backend serializer is
[`ProjectionDefinitionJsonConverter.WriteField`](./src/Namh.Data.DynamicSql/Contracts/QueryDefinitions/Parsers/ProjectionDefinitionJsonConverter.cs).
The fixture below is also covered by
[`SubGroups_JsonDeserialize`](./src/Namh.Data.DynamicSql.Test/UnitTest/40.QueryDefintionTest.cs).

## Group and operator shape

- A root group is written with an enum-name `oper`, `kind: "group"`, and
  `fields`.
- A nested group is written with `kind: "group"` and `arguments`; a non-add
  group operation is written as the symbol-valued `operator` property.
- A math term is written with an enum-name `oper`, `kind: "math"`, and a
  math-compatible `field`.
- A constant such as `<3>` is represented by a `value` node with numeric
  `value: 3`.

The enum names shown here (for example, `"Add"` and `"Subtract"`) require
`JsonStringEnumConverter` in the serializer options. The nested group's
`operator` remains a symbol (for example, `"-"`).

## Example

The canonical JSON for `A - B * (C - D) / E + <3>` is:

```json
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
```

The outer group is additive: its second field is a subtractive group and its
third field is an explicit add term. Inside the subtractive group, the nested
multiplicative group keeps `C - D` together, while the divide term remains
separate. This nesting preserves operator precedence and parentheses.
