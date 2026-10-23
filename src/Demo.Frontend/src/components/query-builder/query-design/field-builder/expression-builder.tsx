import { useMemo, useState } from "react";
import type { PointerEvent as ReactPointerEvent } from "react";
import type {
  IFieldDefinition,
  MatchOperation,
  MetadataEntity,
} from "../query-builder-slice";
import FieldBuilder from "./field-builder";
import ExpressionDebugTooltip from "./expression-debug-tooltip/expression-debug-tooltip";
import { FieldBoxPopupContext } from "./field-box/field-box-popup-context";
import Icon from "../../../icon/icon";
import type { FunctionDefinition } from "../../../metadata-builder/function-definition/types";
import "./expression-builder.css";

type ExpressionBuilderProps = {
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  functionDefinitions: FunctionDefinition[];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  initialKind?: "group" | "function";
  initialExpression?: IFieldDefinition;
  onApply: (expression: IFieldDefinition) => void;
};

type ExpressionNodeKind = "field" | "value" | "function" | "group";

const mathOperatorLabels: Record<MatchOperation, string> = {
  "+": "ADD",
  "-": "SUB",
  "*": "MUL",
  "/": "DIV",
  "%": "MOD",
  "&": "AND",
  "|": "OR",
};
const mathOperators: MatchOperation[] = ["+", "-", "*", "/", "%", "&", "|"];

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

function createExpression(kind: ExpressionNodeKind, functions: FunctionDefinition[]): IFieldDefinition {
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

function withOperation(expression: IFieldDefinition, oper: MatchOperation): IFieldDefinition {
  if (expression.kind === "group") return { ...expression, operator: oper };
  if (expression.kind === "math") return { ...expression, oper };
  return { kind: "math", oper, field: expression };
}

type ExpressionPath = number[];

type ExpressionPointerDrag = {
  sourcePath: ExpressionPath;
  x: number;
  y: number;
};

type ExpressionDropTarget =
  | { kind: "field"; path: ExpressionPath; position: "before" | "after" }
  | { kind: "group"; path: ExpressionPath };

function isExpressionGroup(expression: IFieldDefinition): expression is Extract<IFieldDefinition, { kind: "group" }> {
  return expression.kind === "group";
}

function getExpressionNode(expression: IFieldDefinition, path: ExpressionPath): IFieldDefinition | undefined {
  let node = expression;
  for (const index of path) {
    if (!isExpressionGroup(node) || !node.arguments[index]) return undefined;
    node = node.arguments[index];
  }
  return node;
}

function updateExpressionGroup(
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

function replaceExpressionNode(
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

function setExpressionOperation(expression: IFieldDefinition, operation: MatchOperation | null): IFieldDefinition {
  if (isExpressionGroup(expression)) return { ...expression, operator: operation ?? undefined };
  if (expression.kind === "math") {
    return operation ? { ...expression, oper: operation } : expression.field;
  }
  if (!operation) return expression;
  return { kind: "math", oper: operation, field: expression };
}

function isSameExpressionPath(first: ExpressionPath, second: ExpressionPath): boolean {
  return first.length === second.length && first.every((index, pathIndex) => index === second[pathIndex]);
}

function isExpressionPath(value: unknown): value is ExpressionPath {
  return Array.isArray(value) && value.every((index) => Number.isInteger(index) && index >= 0);
}

function adjustExpressionPathAfterRemoval(sourcePath: ExpressionPath, targetPath: ExpressionPath): ExpressionPath {
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

function getExpressionDropTarget(x: number, y: number): ExpressionDropTarget | undefined {
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
  const group = element.closest<HTMLElement>(
    ".expression-group-list[data-expression-path]",
  );
  if (!group) return undefined;
  const path = JSON.parse(group.dataset.expressionPath ?? "null") as unknown;
  return isExpressionPath(path) ? { kind: "group", path } : undefined;
}

function updateFunction(
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
      return String(expression.value ?? "");
    case "math":
      return `${expression.oper} ${formatOperand(expression.field)}`;
    case "group":
      return expression.arguments.map((argument, index) => {
        if (index === 0) return formatOperand(argument);
        if (argument.kind === "math")
          return `${argument.oper} ${formatOperand(argument.field)}`;
        const operation = argument.kind === "group"
          ? argument.operator ?? expression.operator ?? "+"
          : expression.operator ?? "+";
        return `${operation} ${formatOperand(argument)}`;
      }).join(" ");
    case "function":
      return `${expression.function || "Choose function"}(${expression.arguments.map(formatOperand).join(", ")})`;
  }
}

function getExpressionError(expression: IFieldDefinition): string | null {
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

function getGroupArgumentOperator(
  expression: Extract<IFieldDefinition, { kind: "group" }>,
  argument: IFieldDefinition,
  index: number,
): MatchOperation | null {
  if (argument.kind === "math") return argument.oper;
  if (index === 0) return null;
  return argument.kind === "group"
    ? argument.operator ?? expression.operator ?? "+"
    : expression.operator ?? "+";
}

type NodeEditorProps = {
  expression: IFieldDefinition;
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  functionDefinitions: FunctionDefinition[];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  onChange: (expression: IFieldDefinition) => void;
  onCalculationSelect?: () => void;
  onRemove?: () => void;
  debugOperator?: MatchOperation | null;
  path?: ExpressionPath;
  onPointerDragStart?: (event: ReactPointerEvent<HTMLDivElement>, path: ExpressionPath) => void;
  dragPreview?: ExpressionPath;
  dragPosition?: "before" | "after";
  root?: boolean;
};

type ExpressionMathElementProps = Omit<NodeEditorProps, "expression" | "root" | "debugOperator"> & {
  expression: IFieldDefinition;
  operator?: MatchOperation | null;
};

function ExpressionMathElement({
  expression,
  operator,
  entity,
  metadata,
  functionDefinitions,
  functionDefinitionsStatus,
  functionDefinitionsError,
  onChange,
  onCalculationSelect,
  path,
  onPointerDragStart,
  dragPreview,
  dragPosition,
}: ExpressionMathElementProps) {
  const isMath = expression.kind === "math";
  const operand = isMath ? expression.field : expression;
  const actualOperator = isMath ? expression.oper : operator;
  const updateOperator = (nextOperator: MatchOperation) => {
    onChange(isMath
      ? { ...expression, oper: nextOperator }
      : withOperation(expression, nextOperator));
  };
  const updateOperand = (next: IFieldDefinition) => {
    if (isMath) {
      if (next.kind !== "group") onChange({ ...expression, field: next });
      return;
    }
    onChange(next);
  };

  return (
    <div className={`expression-math-element${isMath ? " expression-debug-target expression-debug-math-box" : ""}`}>
      <div
        className="expression-math-operation"
      >
        {actualOperator && (
          <select
            className="expression-math-operation-select"
            aria-label="Expression operator"
            value={actualOperator}
            onChange={(event) => {
              const nextOperator = mathOperators.find((item) => item === event.target.value);
              if (nextOperator) updateOperator(nextOperator);
            }}
          >
            {mathOperators.map((mathOperator) => (
              <option key={mathOperator} value={mathOperator}>{mathOperatorLabels[mathOperator]}</option>
            ))}
          </select>
        )}
      </div>
      <div
        className={`expression-math-field${operand.kind === "group" ? " expression-math-field-group" : " expression-math-field-draggable"}`}
        onPointerDown={path === undefined
          ? undefined
          : (event) => {
            const bounds = event.currentTarget.getBoundingClientRect();
            const dragEdge = bounds.left + (operand.kind === "group" ? 16 : 0);
            if (event.clientX >= dragEdge && event.clientX <= dragEdge + 8)
              onPointerDragStart?.(event, path);
          }}
      >
        <NodeEditor
          expression={operand}
          entity={entity}
          metadata={metadata}
          functionDefinitions={functionDefinitions}
          functionDefinitionsStatus={functionDefinitionsStatus}
          functionDefinitionsError={functionDefinitionsError}
          onChange={updateOperand}
          onCalculationSelect={onCalculationSelect}
          path={path}
          onPointerDragStart={onPointerDragStart}
          dragPreview={dragPreview}
          dragPosition={dragPosition}
          debugOperator={actualOperator}
        />
      </div>
      {isMath && <ExpressionDebugTooltip label="Math box" model={expression} />}
    </div>
  );
}

function NodeEditor({
  expression,
  entity,
  metadata,
  functionDefinitions,
  functionDefinitionsStatus,
  functionDefinitionsError,
  onChange,
  onCalculationSelect,
  onRemove,
  path,
  onPointerDragStart,
  dragPreview,
  dragPosition,
  debugOperator,
  root = false,
}: NodeEditorProps) {
  const isDropPreview = path !== undefined && dragPreview !== undefined && isSameExpressionPath(path, dragPreview);
  if (expression.kind === "field") {
    return (
      <div
        className={`expression-field-drop-target${isDropPreview ? " expression-drop-preview" : ""}${isDropPreview && dragPosition ? ` expression-drop-position-${dragPosition}` : ""}`}
        data-expression-path={path === undefined ? undefined : JSON.stringify(path)}
      >
        <div className="expression-debug-target expression-debug-field-builder">
          <FieldBuilder
            entity={entity}
            metadata={metadata}
            value={expression.field}
            onChange={(field) => onChange({ kind: "field", field })}
            showPath
            ariaLabel="Expression field"
            placeholder="Select field..."
            allowValueSelection
            allowCalculationSelection
            initialSection="fields"
            expressionBuilder={{
              functionDefinitions,
              functionDefinitionsStatus,
              functionDefinitionsError,
            }}
            debugModel={expression}
            debugOperator={debugOperator}
            onCalculationSelect={onCalculationSelect}
            onValueSelect={(value) => onChange({ kind: "value", value })}
            onExpressionApply={onChange}
          />
          <ExpressionDebugTooltip label="Field builder" model={expression} />
        </div>
      </div>
    );
  }
  if (expression.kind === "group") {
    const groupDebugModel: IFieldDefinition = expression.operator === undefined && debugOperator != null
      ? { ...expression, operator: debugOperator }
      : expression;
    return (
      <div
        className={`expression-group-list expression-debug-target expression-debug-group-box${root ? " expression-group-root" : " expression-group-nested"}${isDropPreview ? " expression-drop-preview" : ""}`}
        data-expression-path={path === undefined ? undefined : JSON.stringify(path)}
      >
        {expression.arguments.map((argument, index) => {
          const argumentPath = path === undefined ? undefined : [...path, index];
          const updateArgument = (next: IFieldDefinition) => {
            const argumentsList = expression.arguments.map((item, itemIndex) =>
              itemIndex === index ? next : item,
            );
            onChange({ ...expression, arguments: argumentsList });
          };
          const appendMath = () => onChange({
            ...expression,
            arguments: [
              ...expression.arguments.slice(0, index + 1),
              { kind: "math", oper: "+", field: { kind: "field", field: "" } },
              ...expression.arguments.slice(index + 1),
            ],
          });

          return (
            <ExpressionMathElement
              key={`expression-item-${index}`}
              expression={argument}
              operator={getGroupArgumentOperator(expression, argument, index)}
              entity={entity}
              metadata={metadata}
              functionDefinitions={functionDefinitions}
              functionDefinitionsStatus={functionDefinitionsStatus}
              functionDefinitionsError={functionDefinitionsError}
              path={argumentPath}
              onPointerDragStart={onPointerDragStart}
              dragPreview={dragPreview}
              dragPosition={dragPosition}
              onChange={updateArgument}
              onCalculationSelect={appendMath}
            />
          );
        })}
        <ExpressionDebugTooltip label="Group box" model={groupDebugModel} />
      </div>
    );
  }
  if (expression.kind === "math") {
    return (
      <ExpressionMathElement
        expression={expression}
        entity={entity}
        metadata={metadata}
        functionDefinitions={functionDefinitions}
        functionDefinitionsStatus={functionDefinitionsStatus}
        functionDefinitionsError={functionDefinitionsError}
        path={path}
        onPointerDragStart={onPointerDragStart}
        dragPreview={dragPreview}
        dragPosition={dragPosition}
        onChange={onChange}
        onCalculationSelect={onCalculationSelect}
      />
    );
  }
  if (expression.kind === "value") {
    const constantValue = typeof expression.value === "string" || typeof expression.value === "number"
      ? expression.value
      : String(expression.value ?? "");
    return (
      <div
        className="expression-debug-target expression-value-node"
        data-expression-kind={expression.kind}
        data-expression-path={path === undefined ? undefined : JSON.stringify(path)}
      >
        <FieldBuilder
          entity={entity}
          metadata={metadata}
          value={`<${constantValue}>`}
          onChange={(field) => onChange({ kind: "field", field })}
          showPath
          ariaLabel="Expression constant"
          initialConstantValue={constantValue}
          initialSection="value"
          allowValueSelection
          allowCalculationSelection
          expressionBuilder={{
            functionDefinitions,
            functionDefinitionsStatus,
            functionDefinitionsError,
          }}
          debugModel={expression}
          onCalculationSelect={onCalculationSelect}
          onValueSelect={(value) => onChange({ ...expression, value })}
          onExpressionApply={onChange}
        />
        {onRemove && (
          <button
            type="button"
            className="expression-remove"
            aria-label="Remove expression item"
            title="Remove expression item"
            onClick={onRemove}
          >
            <Icon name="close" size={13} />
          </button>
        )}
        <ExpressionDebugTooltip label="Field box" model={expression} />
      </div>
    );
  }
  const title = "FUNCTION";

  return (
    <div
      className={`expression-node expression-node-${expression.kind}${root ? " expression-node-root" : ""}`}
      data-expression-kind={expression.kind}
      data-expression-path={path === undefined ? undefined : JSON.stringify(path)}
    >
      <div className="expression-node-heading">
        <span className={`expression-node-kind expression-node-kind-${expression.kind}`}>
          {expression.kind === "function" && <Icon name="code" size={12} />}
          {title}
        </span>
        {onRemove && (
          <button
            type="button"
            className="expression-remove"
            aria-label="Remove expression item"
            title="Remove expression item"
            onClick={onRemove}
          >
            <Icon name="close" size={13} />
          </button>
        )}
      </div>

      {expression.kind === "function" && (
        <div className="expression-function-editor">
          <label className="expression-function-select">
            <span>Function</span>
            <select
              aria-label="Function"
              value={expression.function}
              onChange={(event) => onChange(updateFunction(expression, event.target.value, functionDefinitions))}
            >
              <option value="">Choose a function…</option>
              {expression.function && !functionDefinitions.some((item) => item.name === expression.function) && (
                <option value={expression.function}>{expression.function}</option>
              )}
              {functionDefinitions.map((definition) => (
                <option key={definition.name} value={definition.name}>{definition.name}</option>
              ))}
            </select>
          </label>
          {expression.arguments.length > 0 && (
            <div className="expression-function-arguments">
              {expression.arguments.map((argument, index) => {
                const definition = functionDefinitions.find((item) => item.name === expression.function);
                const argumentDefinition = definition?.arguments[index];
                return (
                  <div className="expression-function-argument" key={`${expression.function}-${index}`}>
                    <div className="expression-function-argument-label">
                      <span>{argumentDefinition?.name || `Argument ${index + 1}`}</span>
                      {argumentDefinition && <small>{argumentDefinition.dataType}</small>}
                    </div>
                    <NodeEditor
                      expression={argument}
                      entity={entity}
                      metadata={metadata}
                      functionDefinitions={functionDefinitions}
                      functionDefinitionsStatus={functionDefinitionsStatus}
                      functionDefinitionsError={functionDefinitionsError}
                      onChange={(next) => onChange({
                        ...expression,
                        arguments: expression.arguments.map((item, itemIndex) => itemIndex === index ? next : item),
                      })}
                      onCalculationSelect={onCalculationSelect}
                    />
                  </div>
                );
              })}
            </div>
          )}
          {expression.arguments.length === 0 && expression.function && (
            <p className="expression-function-empty">This function takes no arguments.</p>
          )}
        </div>
      )}

    </div>
  );
}

export default function ExpressionBuilder({
  entity,
  metadata,
  functionDefinitions,
  functionDefinitionsStatus,
  functionDefinitionsError,
  initialKind,
  initialExpression,
  onApply,
}: ExpressionBuilderProps) {
  const [activeFieldBoxPopupId, setActiveFieldBoxPopupId] = useState<symbol | null>(null);
  const localPopupManager = useMemo(
    () => ({ activeMenuId: activeFieldBoxPopupId, setActiveMenuId: setActiveFieldBoxPopupId }),
    [activeFieldBoxPopupId],
  );
  const popupManager = localPopupManager;
  const [expression, setExpression] = useState<IFieldDefinition>(
    initialExpression ?? createExpression(initialKind === "function" ? "function" : "group", functionDefinitions),
  );
  const [pointerDrag, setPointerDrag] = useState<ExpressionPointerDrag>();
  const [dragPreview, setDragPreview] = useState<ExpressionPath>();
  const [dragPosition, setDragPosition] = useState<"before" | "after">();
  const [copyFeedback, setCopyFeedback] = useState<{
    json: string;
    status: "copied" | "error";
  }>();
  const modelJson = JSON.stringify(expression, null, 2);
  const copyStatus = copyFeedback?.json === modelJson ? copyFeedback.status : undefined;
  const validationError = getExpressionError(expression);

  const copyModelJson = async () => {
    try {
      await navigator.clipboard.writeText(modelJson);
      setCopyFeedback({ json: modelJson, status: "copied" });
    } catch {
      setCopyFeedback({ json: modelJson, status: "error" });
    }
  };

  const groupNodeOnField = (
    sourcePath: ExpressionPath,
    targetPath: ExpressionPath,
    position: "before" | "after",
  ) => {
    if (!sourcePath.length || !targetPath.length || isSameExpressionPath(sourcePath, targetPath)
      || (sourcePath.length < targetPath.length
        && sourcePath.every((index, pathIndex) => index === targetPath[pathIndex]))) return;
    const sourceNode = getExpressionNode(expression, sourcePath);
    const sourceParentPath = sourcePath.slice(0, -1);
    const sourceParent = getExpressionNode(expression, sourceParentPath);
    const sourceIndex = sourcePath[sourcePath.length - 1];
    const targetNode = getExpressionNode(expression, targetPath);
    const targetParent = getExpressionNode(expression, targetPath.slice(0, -1));
    const targetIndex = targetPath[targetPath.length - 1];
    if (!sourceNode || !isExpressionGroup(sourceParent) || sourceParent.arguments.length <= 1
      || sourceIndex === undefined || !targetNode || !isExpressionGroup(targetParent)
      || targetIndex === undefined) return;
    const targetField = targetNode.kind === "math" ? targetNode.field : targetNode;
    if (targetField.kind !== "field") return;

    const sourceOperation = getGroupArgumentOperator(sourceParent, sourceNode, sourceIndex);
    const targetOperation = getGroupArgumentOperator(targetParent, targetNode, targetIndex);
    const sourceComesFirst = position === "before";
    const removed = updateExpressionGroup(expression, sourceParentPath, (group) => ({
      ...group,
      arguments: group.arguments.filter((_, index) => index !== sourceIndex),
    }));
    const adjustedTargetPath = adjustExpressionPathAfterRemoval(sourcePath, targetPath);
    const adjustedTarget = getExpressionNode(removed, adjustedTargetPath);
    if (!adjustedTarget) return;
    const adjustedTargetField = adjustedTarget.kind === "math" ? adjustedTarget.field : adjustedTarget;
    if (adjustedTargetField.kind !== "field") return;
    const sourceField = sourceNode.kind === "math" ? sourceNode.field : sourceNode;
    const firstField = sourceComesFirst ? sourceField : adjustedTargetField;
    const secondField = sourceComesFirst ? adjustedTargetField : sourceField;
    const firstOperation = sourceComesFirst ? sourceOperation : targetOperation;
    const secondOperation = sourceComesFirst ? targetOperation : sourceOperation;
    const grouped: IFieldDefinition = {
      kind: "group",
      ...(firstOperation ? { operator: firstOperation } : {}),
      arguments: [
        firstField,
        setExpressionOperation(secondField, secondOperation),
      ],
    };
    setExpression(replaceExpressionNode(removed, adjustedTargetPath, grouped));
  };

  const moveNode = (sourcePath: ExpressionPath, targetPath: ExpressionPath) => {
    if (!sourcePath.length || sourcePath.length <= targetPath.length
      && sourcePath.every((index, pathIndex) => index === targetPath[pathIndex])) return;
    const node = getExpressionNode(expression, sourcePath);
    if (!node) return;
    const removed = updateExpressionGroup(expression, sourcePath.slice(0, -1), (parent) => ({
      ...parent,
      arguments: parent.arguments.filter((_, index) => index !== sourcePath[sourcePath.length - 1]),
    }));
    const adjustedTargetPath = adjustExpressionPathAfterRemoval(sourcePath, targetPath);
    const target = getExpressionNode(removed, adjustedTargetPath);
    if (!target || !isExpressionGroup(target)) return;
    const lastIndex = target.arguments.length - 1;
    const operation = lastIndex < 0
      ? null
      : target.operator
        ?? getGroupArgumentOperator(target, target.arguments[lastIndex]!, lastIndex)
        ?? "+";
    const moved = setExpressionOperation(node, operation);
    setExpression(updateExpressionGroup(removed, adjustedTargetPath, (group) => ({
      ...group,
      arguments: [...group.arguments, moved],
    })));
  };

  const startPointerDrag = (event: ReactPointerEvent<HTMLDivElement>, sourcePath: ExpressionPath) => {
    if (event.button !== 0) return;
    event.preventDefault();
    event.stopPropagation();
    event.currentTarget.setPointerCapture(event.pointerId);
    setPointerDrag({ sourcePath, x: event.clientX, y: event.clientY });
    setDragPreview(undefined);
    setDragPosition(undefined);
  };

  const updatePointerDrag = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!pointerDrag) return;
    event.preventDefault();
    const target = getExpressionDropTarget(event.clientX, event.clientY);
    setPointerDrag({ ...pointerDrag, x: event.clientX, y: event.clientY });
    setDragPreview(target?.path);
    setDragPosition(target?.kind === "field" ? target.position : undefined);
  };

  const finishPointerDrag = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!pointerDrag) return;
    const target = getExpressionDropTarget(event.clientX, event.clientY);
    if (target?.kind === "field") {
      groupNodeOnField(pointerDrag.sourcePath, target.path, target.position);
    } else if (target?.kind === "group") {
      moveNode(pointerDrag.sourcePath, target.path);
    }
    setPointerDrag(undefined);
    setDragPreview(undefined);
    setDragPosition(undefined);
  };

  const cancelPointerDrag = () => {
    setPointerDrag(undefined);
    setDragPreview(undefined);
    setDragPosition(undefined);
  };

  const draggedNode = pointerDrag ? getExpressionNode(expression, pointerDrag.sourcePath) : undefined;

  return (
    <FieldBoxPopupContext.Provider value={popupManager}>
      <div
        className="expression-builder"
        onPointerMove={updatePointerDrag}
        onPointerUp={finishPointerDrag}
        onPointerCancel={cancelPointerDrag}
      >
      <NodeEditor
        expression={expression}
        entity={entity}
        metadata={metadata}
        functionDefinitions={functionDefinitions}
        functionDefinitionsStatus={functionDefinitionsStatus}
        functionDefinitionsError={functionDefinitionsError}
        path={[]}
        onPointerDragStart={startPointerDrag}
        dragPreview={dragPreview}
        dragPosition={dragPosition}
        onChange={setExpression}
        root
      />

      {pointerDrag && draggedNode && (
        <div
          className="expression-drag-preview"
          style={{ left: pointerDrag.x + 12, top: pointerDrag.y + 12 }}
          aria-hidden="true"
        >
          {formatExpression(draggedNode)}
        </div>
      )}

      <div className="expression-preview" aria-live="polite">
        <span className="expression-preview-label">PREVIEW</span>
        <code>{formatExpression(expression)}</code>
        <div className="expression-preview-json-heading">
          <span className="expression-preview-label">MODEL JSON</span>
          <button
            type="button"
            className="expression-preview-copy"
            aria-label="Copy JSON model"
            title="Copy JSON model"
            onClick={copyModelJson}
          >
            <Icon name={copyStatus === "copied" ? "check" : "copy"} size={13} />
          </button>
          {copyStatus && (
            <span className={`expression-preview-copy-status${copyStatus === "error" ? " expression-preview-copy-error" : ""}`} role="status">
              {copyStatus === "copied" ? "Copied" : "Copy failed. Check clipboard permissions."}
            </span>
          )}
        </div>
        <pre className="expression-preview-json"><code>{modelJson}</code></pre>
      </div>

      <footer className="expression-builder-footer">
        <div className="expression-builder-actions">
          <button
            type="button"
            className="expression-apply-button"
            disabled={Boolean(validationError)}
            onClick={() => onApply(expression)}
          >
            <Icon name="check" size={14} />
            Apply
          </button>
        </div>
      </footer>
      </div>
    </FieldBoxPopupContext.Provider>
  );
}
