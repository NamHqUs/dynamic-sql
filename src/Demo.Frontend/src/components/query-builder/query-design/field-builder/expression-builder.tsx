import { useState } from "react";
import type {
  IFieldDefinition,
  MatchOperation,
  MetadataEntity,
} from "../query-builder-slice";
import FieldBuilder from "./field-builder";
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

const operations: { value: MatchOperation; label: string }[] = [
  { value: "+", label: "+ Add" },
  { value: "-", label: "− Subtract" },
  { value: "*", label: "× Multiply" },
  { value: "/", label: "÷ Divide" },
  { value: "%", label: "% Modulus" },
  { value: "&", label: "& Bitwise AND" },
  { value: "|", label: "| Bitwise OR" },
];

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
        arguments: [{ kind: "field", field: "" }, { kind: "field", field: "" }],
      };
  }
}

function withOperation(expression: IFieldDefinition, oper: MatchOperation): IFieldDefinition {
  if (expression.kind === "group") return { ...expression, operator: oper };
  if (expression.kind === "math") return { ...expression, oper };
  return { kind: "math", oper, field: expression };
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
      return `(${expression.arguments.map((argument, index) => {
        if (index === 0) return formatOperand(argument);
        if (argument.kind === "math")
          return `${argument.oper} ${formatOperand(argument.field)}`;
        const operation = argument.kind === "group"
          ? argument.operator ?? expression.operator ?? "+"
          : expression.operator ?? "+";
        return `${operation} ${formatOperand(argument)}`;
      }).join(" ")})`;
    case "function":
      return `${expression.function || "Choose function"}(${expression.arguments.map(formatExpression).join(", ")})`;
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

type NodeEditorProps = {
  expression: IFieldDefinition;
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  functionDefinitions: FunctionDefinition[];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  onChange: (expression: IFieldDefinition) => void;
  onRemove?: () => void;
  root?: boolean;
  allowGroup?: boolean;
};

function NodeEditor({
  expression,
  entity,
  metadata,
  functionDefinitions,
  functionDefinitionsStatus,
  functionDefinitionsError,
  onChange,
  onRemove,
  root = false,
  allowGroup = true,
}: NodeEditorProps) {
  const replaceWith = (kind: ExpressionNodeKind) => onChange(createExpression(kind, functionDefinitions));
  if (expression.kind === "field") {
    return (
      <FieldBuilder
        entity={entity}
        metadata={metadata}
        value={expression.field}
        onChange={(field) => onChange({ kind: "field", field })}
        showPath
        ariaLabel="Expression field"
        placeholder="Choose a field…"
        allowValueSelection
        allowCalculationSelection
        initialSection="fields"
        expressionBuilder={{
          functionDefinitions,
          functionDefinitionsStatus,
          functionDefinitionsError,
        }}
        onValueSelect={(value) => onChange({ kind: "value", value })}
        onExpressionApply={onChange}
      />
    );
  }
  if (root && expression.kind === "group") {
    return (
      <div className="expression-builder-tree">
        {expression.arguments.map((argument, index) => (
          <span className="expression-builder-tree-item" key={`tree-item-${index}`}>
            {index > 0 && <span className="expression-builder-tree-operator">+</span>}
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
            />
          </span>
        ))}
      </div>
    );
  }
  const title = expression.kind === "math"
    ? "OPERATION"
    : expression.kind === "group"
      ? "GROUP"
      : expression.kind === "function"
        ? "FUNCTION"
        : expression.kind === "value"
          ? "CONSTANT"
          : "FIELD";

  return (
    <div className={`expression-node expression-node-${expression.kind}${root ? " expression-node-root" : ""}`}>
      <div className="expression-node-heading">
        <span className={`expression-node-kind expression-node-kind-${expression.kind}`}>
          {expression.kind === "function" && <Icon name="code" size={12} />}
          {title}
        </span>
        {expression.kind === "math" && (
          <label className="expression-operation-label">
            <span className="visually-hidden">Operation</span>
            <select
              aria-label="Math operation"
              value={expression.oper}
              onChange={(event) => onChange({
                ...expression,
                oper: event.target.value as MatchOperation,
              })}
            >
              {operations.map((operation) => (
                <option key={operation.value} value={operation.value}>{operation.label}</option>
              ))}
            </select>
          </label>
        )}
        {!root && (
          <select
            className="expression-node-replace"
            aria-label="Change expression item type"
            value=""
            onChange={(event) => replaceWith(event.target.value as ExpressionNodeKind)}
          >
            <option value="" disabled>Change type…</option>
            <option value="field">Field</option>
            <option value="value">Constant</option>
            <option value="function">Function</option>
            {allowGroup && <option value="group">Group</option>}
          </select>
        )}
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

      {expression.kind === "value" && (
        <div className="expression-node-content expression-value-editor">
          <select
            aria-label="Constant type"
            value={typeof expression.value === "number" ? "number" : "text"}
            onChange={(event) => onChange({
              ...expression,
              value: event.target.value === "number"
                ? Number(expression.value) || 0
                : String(expression.value ?? ""),
            })}
          >
            <option value="number">Number</option>
            <option value="text">Text</option>
          </select>
          <input
            aria-label="Constant value"
            type={typeof expression.value === "number" ? "number" : "text"}
            step="any"
            placeholder="Enter a value"
            value={String(expression.value ?? "")}
            onChange={(event) => onChange({
              ...expression,
              value: typeof expression.value === "number"
                ? (event.target.value === "" ? "" : Number(event.target.value))
                : event.target.value,
            })}
          />
        </div>
      )}

      {expression.kind === "math" && (
        <div className="expression-node-content expression-math-operand">
          <span className="expression-operand-prefix">WITH</span>
          <NodeEditor
            expression={expression.field}
            entity={entity}
            metadata={metadata}
            functionDefinitions={functionDefinitions}
            functionDefinitionsStatus={functionDefinitionsStatus}
            functionDefinitionsError={functionDefinitionsError}
            allowGroup={false}
            onChange={(field) => {
              if (field.kind !== "group") onChange({ ...expression, field });
            }}
          />
        </div>
      )}

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

      {expression.kind === "group" && (
        <div className="expression-group-editor">
          <div className="expression-group-terms">
            {expression.arguments.map((argument, index) => (
              <div className="expression-term" key={`term-${index}`}>
                {index === 0
                  ? <span className="expression-term-start">START</span>
                  : argument.kind === "math"
                    ? <span className="expression-term-operator" aria-hidden="true">{argument.oper}</span>
                    : (
                      <label className="expression-term-operator-select">
                        <span className="visually-hidden">Operation before item {index + 1}</span>
                        <select
                          aria-label={`Operation before item ${index + 1}`}
                          value={argument.kind === "group"
                            ? argument.operator ?? expression.operator ?? "+"
                            : expression.operator ?? "+"}
                          onChange={(event) => {
                            const operation = event.target.value as MatchOperation;
                            const next = argument.kind === "group"
                              ? { ...argument, operator: operation }
                              : withOperation(argument, operation);
                            onChange({
                              ...expression,
                              arguments: expression.arguments.map((item, itemIndex) => itemIndex === index ? next : item),
                            });
                          }}
                        >
                          {operations.map((operation) => (
                            <option key={operation.value} value={operation.value}>{operation.label}</option>
                          ))}
                        </select>
                      </label>
                    )}
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
                  onRemove={expression.arguments.length > 1
                    ? () => onChange({
                      ...expression,
                      arguments: expression.arguments.filter((_, itemIndex) => itemIndex !== index),
                    })
                    : undefined}
                />
              </div>
            ))}
          </div>
          <div className="expression-add-actions" aria-label="Add expression item">
            <span className="expression-add-label">ADD</span>
            {([
              ["field", "Field"],
              ["value", "Constant"],
              ["function", "Function"],
              ["group", "Group"],
            ] as const).map(([kind, label]) => (
              <button
                type="button"
                className="expression-add-button"
                key={kind}
                disabled={kind === "function" && functionDefinitions.length === 0}
                onClick={() => {
                  const added = createExpression(kind, functionDefinitions);
                  const next = expression.arguments.length > 0
                    ? withOperation(added, "+")
                    : added;
                  onChange({ ...expression, arguments: [...expression.arguments, next] });
                }}
              >
                <Icon name="plus" size={12} />
                {label}
              </button>
            ))}
          </div>
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
  const [expression, setExpression] = useState<IFieldDefinition>(
    initialExpression ?? createExpression(initialKind === "function" ? "function" : "group", functionDefinitions),
  );
  const validationError = getExpressionError(expression);

  return (
    <div className="expression-builder">
      <div className="expression-preview" aria-live="polite">
        <span className="expression-preview-label">PREVIEW</span>
        <code>{formatExpression(expression)}</code>
      </div>

      <NodeEditor
        expression={expression}
        entity={entity}
        metadata={metadata}
        functionDefinitions={functionDefinitions}
        functionDefinitionsStatus={functionDefinitionsStatus}
        functionDefinitionsError={functionDefinitionsError}
        onChange={setExpression}
        root
      />

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
  );
}
