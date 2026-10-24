# Expression Model JSON Reference

This document records the canonical JSON shape for backend projection
expressions. A projection expression is a flat `fields` array; there is no
root group and no `math` wrapper. This differs from the frontend TypeScript
expression type.

The backend serializer is
[`ProjectionDefinitionJsonConverter`](./src/Namh.Data.DynamicSql/Contracts/QueryDefinitions/Parsers/ProjectionDefinitionJsonConverter.cs).
The format is covered by
[`ProjectionJsonWritesOperatorOnEachExpressionNode`](./src/Namh.Data.DynamicSql.Test/UnitTest/40.QueryDefintionTest.cs).

## Nodes and operators

- Each field, value, function, or group node has an `oper` property in the
  model. JSON omits `oper` for the default `Add` operator and writes the
  operator symbol (for example, `"-"` or `"*"`) when it is not `Add`.
- A projection contains a `fields` array. The operator on each later item
  connects it to the preceding expression item.
- A group node has its own `oper` for how the group participates in its
  containing expression, and a `fields` array for the group's contents.
- Function arguments remain an array. An arithmetic argument is represented
  by a group; a single field or value is represented directly.
- Constants use `kind: "value"` and a scalar `value`.

## Flat expression

`A + B - <3>` is represented without a root group or math wrappers:

```json
{
  "fields": [
    { "kind": "field", "field": "A" },
    { "kind": "field", "field": "B" },
    { "oper": "-", "kind": "value", "value": 3 }
  ],
  "alias": null
}
```

## Parenthesized expression

For `A * (B - C + D) / <2>`, the grouped term is a node in the projection
list. Its operation is multiplication; the operators inside its `fields`
array describe `B - C + D`:

```json
{
  "fields": [
    { "kind": "field", "field": "A" },
    {
      "oper": "*",
      "kind": "group",
      "fields": [
        { "kind": "field", "field": "B" },
        { "oper": "-", "kind": "field", "field": "C" },
        { "kind": "field", "field": "D" }
      ]
    },
    { "oper": "/", "kind": "value", "value": 2 }
  ],
  "alias": null
}
```

The query compiler applies normal arithmetic precedence to flat terms and
retains parentheses through nested groups.

## Function arguments

`Round(A + B, <2>)` stores its arithmetic first argument as a group and the
constant second argument directly:

```json
{
  "fields": [
    {
      "kind": "function",
      "function": "Round",
      "arguments": [
        {
          "kind": "group",
          "fields": [
            { "kind": "field", "field": "A" },
            { "kind": "field", "field": "B" }
          ]
        },
        { "kind": "value", "value": 2 }
      ]
    }
  ],
  "alias": null
}
```

For `5 - (A - B) * (C - D)`, the two parenthesized operands are sibling
groups, each carrying its operator in the containing list:

```json
{
  "fields": [
    { "kind": "value", "value": 5 },
    {
      "oper": "-",
      "kind": "group",
      "fields": [
        { "kind": "field", "field": "A" },
        { "oper": "-", "kind": "field", "field": "B" }
      ]
    },
    {
      "oper": "*",
      "kind": "group",
      "fields": [
        { "kind": "field", "field": "C" },
        { "oper": "-", "kind": "field", "field": "D" }
      ]
    }
  ],
  "alias": null
}
```

The reader continues to accept legacy projection objects that contain a
singular `field` and legacy `math` nodes. Serialization always writes the
canonical `fields` array and stores operators directly on expression nodes.
