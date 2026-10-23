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
  math-compatible `field`. Its `field` is not a group, and math nodes do not
  have an `arguments` property.
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

## Function calls with parenthesized arithmetic

For the query-builder expression `A + (Round(B, <2>) * (B - <1>) - C) + <4>`,
preserve the nested groups and operand kinds as shown below:

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
      "arguments": [
        {
          "kind": "group",
          "arguments": [
            {
              "kind": "function",
              "function": "Round",
              "arguments": [
                {
                  "kind": "field",
                  "field": "B"
                },
                {
                  "kind": "value",
                  "value": 2
                }
              ]
            },
            {
              "kind": "group",
              "operator": "*",
              "arguments": [
                {
                  "kind": "field",
                  "field": "B"
                },
                {
                  "oper": "Subtract",
                  "kind": "math",
                  "field": {
                    "kind": "value",
                    "value": 1
                  }
                }
              ]
            }
          ]
        },
        {
          "oper": "Subtract",
          "kind": "math",
          "field": {
            "kind": "field",
            "field": "C"
          }
        }
      ]
    },
    {
      "oper": "Add",
      "kind": "math",
      "field": {
        "kind": "value",
        "value": 4
      }
    }
  ]
}
```

Angle brackets mark numeric constants, so `<2>`, `<1>`, and `<4>` are all
represented as `value` nodes. Preserve the inner additive group around the
`Round` call and multiplication group, and represent `- C` as a separate
subtractive math term. Flattening these groups or moving operators changes
the expression structure.

When `+ G * F` follows another term in a group, encode it as a nested
additive group. Keep the multiplication as a math term inside that group's
`arguments`; do not put a group in a math node's `field`:

```json
{
  "kind": "group",
  "arguments": [
    {
      "kind": "field",
      "field": "G"
    },
    {
      "oper": "Multiply",
      "kind": "math",
      "field": {
        "kind": "field",
        "field": "F"
      }
    }
  ]
}
```
