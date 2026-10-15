export type FunctionArgumentKind =
  | "Constant"
  | "QueryExpression"
  | "QueryExpressionOrSqlToken"
  | "SqlType";

export type FunctionArgumentDefaultKind = "Constant" | "SqlToken" | "LogicalType";

export type FunctionArgumentDefinition = {
  name: string;
  kind: FunctionArgumentKind;
  allowedTokens: string[];
  allowedTypes: string[];
  allowedConstants: unknown[];
  optional: boolean;
  defaultKind: FunctionArgumentDefaultKind | null;
  defaultValue: unknown;
};

export type FunctionDefinition = {
  name: string;
  arguments: FunctionArgumentDefinition[];
  resultType: string;
  isAggregate: boolean;
  description: string;
  defaultSqlTemplate: string | null;
  sqlTemplates: Record<string, string>;
};

export type EditableFunctionArgument = FunctionArgumentDefinition & {
  id: string;
  allowedTokensText: string;
  allowedTypesText: string;
  allowedConstantsText: string;
  defaultValueText: string;
};

export type EditableFunctionDefinition = Omit<FunctionDefinition, "arguments" | "sqlTemplates"> & {
  arguments: EditableFunctionArgument[];
  sqlTemplates: Record<string, string>;
  sqlTemplatesText: string;
};

export const emptyFunctionArgument = (): EditableFunctionArgument => ({
  id: `argument-${nextArgumentId++}`,
  name: "",
  kind: "QueryExpression",
  allowedTokens: [],
  allowedTypes: [],
  allowedConstants: [],
  optional: false,
  defaultKind: null,
  defaultValue: null,
  allowedTokensText: "",
  allowedTypesText: "",
  allowedConstantsText: "[]",
  defaultValueText: "",
});

let nextArgumentId = 1;

export const emptyFunction = (): EditableFunctionDefinition => ({
  name: "",
  arguments: [],
  resultType: "string",
  isAggregate: false,
  description: "",
  defaultSqlTemplate: "",
  sqlTemplates: {},
  sqlTemplatesText: "{}",
});

export function toEditableFunction(definition: FunctionDefinition): EditableFunctionDefinition {
  return {
    ...definition,
    defaultSqlTemplate: definition.defaultSqlTemplate || "",
    arguments: definition.arguments.map((argument) => ({
      id: `argument-${nextArgumentId++}`,
      ...argument,
      allowedTokensText: argument.allowedTokens.join(", "),
      allowedTypesText: argument.allowedTypes.join(", "),
      allowedConstantsText: JSON.stringify(argument.allowedConstants, null, 2),
      defaultValueText: argument.defaultKind === null ? "" : JSON.stringify(argument.defaultValue),
    })),
    sqlTemplates: { ...definition.sqlTemplates },
    sqlTemplatesText: JSON.stringify(definition.sqlTemplates, null, 2),
  };
}
