import type {
  IFieldDefinition,
  MatchOperation,
} from "../../query-builder-slice";
import type { FunctionDefinition } from "../../../../metadata-builder/function-definition/types";

export type ExpressionPath = number[];

export type ExpressionPointerDrag = {
  sourcePath: ExpressionPath;
  x: number;
  y: number;
};

export type ExpressionDropTarget =
  | { kind: "field"; path: ExpressionPath; position: "before" | "after" }
  | { kind: "group"; path: ExpressionPath };

type ExpressionNodeKind = "field" | "value" | "function" | "group";

export const mathOperatorLabels: Record<MatchOperation, string> = {
  "+": "ADD",
  "-": "SUB",
  "*": "MUL",
  "/": "DIV",
  "%": "MOD",
  "&": "AND",
  "|": "OR",
};

export const mathOperators: MatchOperation[] = ["+", "-", "*", "/", "%", "&", "|"];

function createFunctionExpression(
  definition: FunctionDefinition | undefined,
  name = definition?.name ?? "",
): IFieldDefinition {
  return {
    kind: "function",
    function: name,
    arguments: definition?.arguments.map(() => ({ kind: "field", field: "" })) ?? [],
  };
}

export function createExpression(kind: ExpressionNodeKind, functions: FunctionDefinition[]): IFieldDefinition {
  switch (kind) {
    case "field":
      return { kind: "field", field: "" };
    case "value":
      return { kind: "value", value: 0 };
    case "function":
      return createFunctionExpression(functions[0]);
    case "group":
      return {
        kind: "group",
        arguments: [
          { kind: "field", field: "" },
          { kind: "math", oper: "+", field: { kind: "field", field: "" } },
        ],
      };
  }
}

export function withOperation(expression: IFieldDefinition, oper: MatchOperation): IFieldDefinition {
  if (expression.kind === "group") return { ...expression, operator: oper };
  if (expression.kind === "math") return { ...expression, oper };
  return { kind: "math", oper, field: expression };
}

export function isExpressionGroup(
  expression: IFieldDefinition,
): expression is Extract<IFieldDefinition, { kind: "group" }> {
  return expression.kind === "group";
}

export function getExpressionNode(expression: IFieldDefinition, path: ExpressionPath): IFieldDefinition | undefined {
  let node = expression;
  for (const index of path) {
    if (!isExpressionGroup(node) || !node.arguments[index]) return undefined;
    node = node.arguments[index];
  }
  return node;
}

export function updateExpressionGroup(
  expression: IFieldDefinition,
  path: ExpressionPath,
  update: (group: Extract<IFieldDefinition, { kind: "group" }>) => IFieldDefinition,
): IFieldDefinition {
  if (!isExpressionGroup(expression)) return expression;
  if (!path.length) return update(expression);
  const [index, ...rest] = path;
  const child = expression.arguments[index];
  if (!child || !isExpressionGroup(child)) return expression;
  const updatedChild = updateExpressionGroup(child, rest, update);
  return {
    ...expression,
    arguments: expression.arguments.map((argument, argumentIndex) =>
      argumentIndex === index ? updatedChild : argument,
    ),
  };
}

export function replaceExpressionNode(
  expression: IFieldDefinition,
  path: ExpressionPath,
  replacement: IFieldDefinition,
): IFieldDefinition {
  if (!path.length) return expression;
  const parentPath = path.slice(0, -1);
  const index = path[path.length - 1];
  if (index === undefined) return expression;
  return updateExpressionGroup(expression, parentPath, (parent) => ({
    ...parent,
    arguments: parent.arguments.map((argument, argumentIndex) =>
      argumentIndex === index ? replacement : argument,
    ),
  }));
}

function removeExpressionNodeAtPath(
  expression: IFieldDefinition,
  path: ExpressionPath,
): IFieldDefinition | undefined {
  if (!path.length) return undefined;
  if (expression.kind !== "group" && expression.kind !== "function") return expression;

  const [index, ...rest] = path;
  if (index === undefined || !expression.arguments[index]) return expression;

  if (rest.length > 0) {
    const updatedArgument = removeExpressionNodeAtPath(expression.arguments[index]!, rest);
    if (!updatedArgument) {
      if (expression.kind === "function") {
        return {
          ...expression,
          arguments: expression.arguments.map((argument, argumentIndex) =>
            argumentIndex === index ? { kind: "field", field: "" } : argument,
          ),
        };
      }
      const argumentsList = expression.arguments.filter((_, argumentIndex) => argumentIndex !== index);
      return argumentsList.length > 0
        ? normalizeGroupArguments(expression, argumentsList)
        : undefined;
    }
    const argumentsList = expression.arguments.map((argument, argumentIndex) =>
      argumentIndex === index ? updatedArgument : argument,
    );
    return expression.kind === "group"
      ? normalizeGroupArguments(expression, argumentsList)
      : { ...expression, arguments: argumentsList };
  }

  if (expression.kind === "function") {
    return {
      ...expression,
      arguments: expression.arguments.map((argument, argumentIndex) =>
        argumentIndex === index ? { kind: "field", field: "" } : argument,
      ),
    };
  }

  const argumentsList = expression.arguments.filter((_, argumentIndex) => argumentIndex !== index);
  return argumentsList.length > 0
    ? normalizeGroupArguments(expression, argumentsList)
    : undefined;
}

function normalizeFirstGroupArgument(argumentsList: IFieldDefinition[]): IFieldDefinition[] {
  const firstArgument = argumentsList[0];
  return firstArgument?.kind === "math"
    ? [firstArgument.field, ...argumentsList.slice(1)]
    : argumentsList;
}

function normalizeGroupArguments(
  group: Extract<IFieldDefinition, { kind: "group" }>,
  argumentsList: IFieldDefinition[],
): IFieldDefinition {
  const normalizedArguments = normalizeFirstGroupArgument(argumentsList);
  if (normalizedArguments.length === 1 && normalizedArguments[0]?.kind === "field")
    return normalizedArguments[0];
  return { ...group, arguments: normalizedArguments };
}

export function removeExpressionNode(
  expression: IFieldDefinition,
  path: ExpressionPath,
): IFieldDefinition {
  return removeExpressionNodeAtPath(expression, path) ?? { kind: "field", field: "" };
}

export function setExpressionOperation(
  expression: IFieldDefinition,
  operation: MatchOperation | null,
): IFieldDefinition {
  if (isExpressionGroup(expression)) return { ...expression, operator: operation ?? undefined };
  if (expression.kind === "math") {
    return operation ? { ...expression, oper: operation } : expression.field;
  }
  if (!operation) return expression;
  return { kind: "math", oper: operation, field: expression };
}

export function isSameExpressionPath(first: ExpressionPath, second: ExpressionPath): boolean {
  return first.length === second.length && first.every((index, pathIndex) => index === second[pathIndex]);
}

export function isExpressionPath(value: unknown): value is ExpressionPath {
  return Array.isArray(value) && value.every((index) => Number.isInteger(index) && index >= 0);
}

export function adjustExpressionPathAfterRemoval(
  sourcePath: ExpressionPath,
  targetPath: ExpressionPath,
): ExpressionPath {
  const adjustedPath = [...targetPath];
  const sharedDepth = Math.min(sourcePath.length, targetPath.length);
  for (let depth = 0; depth < sharedDepth; depth++) {
    const sourceIndex = sourcePath[depth];
    const targetIndex = targetPath[depth];
    if (sourceIndex === undefined || targetIndex === undefined) break;
    if (!sourcePath.slice(0, depth).every((index, pathIndex) => index === targetPath[pathIndex])) return adjustedPath;
    if (sourceIndex < targetIndex) {
      adjustedPath[depth]--;
      return adjustedPath;
    }
    if (sourceIndex > targetIndex) return adjustedPath;
  }
  return adjustedPath;
}

export function getExpressionDropTarget(x: number, y: number): ExpressionDropTarget | undefined {
  const element = document.elementFromPoint(x, y);
  if (!(element instanceof Element)) return undefined;
  const field = element.closest<HTMLElement>(".expression-field-drop-target[data-expression-path]");
  if (field) {
    const path = JSON.parse(field.dataset.expressionPath ?? "null") as unknown;
    if (!isExpressionPath(path)) return undefined;
    const bounds = field.getBoundingClientRect();
    return {
      kind: "field",
      path,
      position: y < bounds.top + bounds.height / 2 ? "before" : "after",
    };
  }
  const group = element.closest<HTMLElement>(".expression-group-list[data-expression-path]");
  if (!group) return undefined;
  const path = JSON.parse(group.dataset.expressionPath ?? "null") as unknown;
  return isExpressionPath(path) ? { kind: "group", path } : undefined;
}

export function updateFunction(
  expression: Extract<IFieldDefinition, { kind: "function" }>,
  name: string,
  definitions: FunctionDefinition[],
) {
  const definition = definitions.find((item) => item.name === name);
  return {
    ...expression,
    function: name,
    arguments: definition
      ? definition.arguments.map((_, index) => expression.arguments[index] ?? { kind: "field", field: "" })
      : expression.arguments,
  };
}

export function formatExpression(expression: IFieldDefinition): string {
  const formatOperand = (operand: IFieldDefinition) => operand.kind === "group"
    ? `(${formatExpression(operand)})`
    : formatExpression(operand);

  switch (expression.kind) {
    case "field":
      return expression.field || "Select field";
    case "value":
      return `<${String(expression.value ?? "")}>`;
    case "math":
      return `${expression.oper} ${formatOperand(expression.field)}`;
    case "group":
      return expression.arguments.map((argument, index) => {
        if (index === 0)
          return formatOperand(argument.kind === "math" ? argument.field : argument);
        if (argument.kind === "math")
          return `${argument.oper} ${formatOperand(argument.field)}`;
        const operation = argument.kind === "group"
          ? argument.operator ?? expression.operator ?? "+"
          : expression.operator ?? "+";
        return `${operation} ${formatOperand(argument)}`;
      }).join(" ");
    case "function":
      return `${expression.function || "Choose function"}(${expression.arguments.map((argument) =>
        argument.kind === "group" ? formatExpression(argument) : formatOperand(argument),
      ).join(", ")})`;
  }
}

export function getExpressionError(expression: IFieldDefinition): string | null {
  switch (expression.kind) {
    case "field":
      return expression.field.trim() ? null : "Choose a field for each field term.";
    case "value":
      return null;
    case "math":
      return expression.field.kind === "math"
        ? "Choose a field, constant, or function as the operation operand."
        : getExpressionError(expression.field);
    case "function":
      if (!expression.function) return "Choose a function.";
      for (const argument of expression.arguments) {
        const error = getExpressionError(argument);
        if (error) return error;
      }
      return null;
    case "group":
      if (expression.arguments.length === 0) return "Add at least one item to each group.";
      for (const argument of expression.arguments) {
        const error = getExpressionError(argument);
        if (error) return error;
      }
      return null;
  }
}

export function getGroupArgumentOperator(
  expression: Extract<IFieldDefinition, { kind: "group" }>,
  argument: IFieldDefinition,
  index: number,
): MatchOperation | null {
  if (index === 0) return null;
  if (argument.kind === "math") return argument.oper;
  return argument.kind === "group"
    ? argument.operator ?? expression.operator ?? "+"
    : expression.operator ?? "+";
}
