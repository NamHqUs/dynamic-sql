import { useEffect, useRef, useState, type MouseEvent as ReactMouseEvent, type PointerEvent as ReactPointerEvent } from "react";
import type {
  IFieldDefinition,
  MatchOperation,
  MetadataEntity,
} from "../../query-builder-slice";
import FieldBuilder from "../field-builder";
import ExpressionDebugTooltip from "../expression-debug-tooltip/expression-debug-tooltip";
import ExpressionAddNextItem from "./expression-add-next-item";
import Icon from "../../../../icon/icon";
import type { FunctionDefinition } from "../../../../metadata-builder/function-definition/types";
import FunctionBuilder from "../function-builder/function-builder";
import {
  getGroupArgumentOperator,
  isSameExpressionPath,
  mathOperatorLabels,
  mathOperators,
  withOperation,
  type ExpressionPath,
} from "./expression-model";
import type { IconName } from "../../../../icon/icon";

const mathOperatorIcons: Record<MatchOperation, IconName> = {
  "+": "plus",
  "-": "minus",
  "*": "multiply",
  "/": "divide",
  "%": "modulo",
  "&": "logical-and",
  "|": "logical-or",
};

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
  dragPosition?: "before" | "inside" | "after";
  root?: boolean;
  selectionPath?: ExpressionPath;
  selectedPath?: ExpressionPath;
  onSelect?: (path: ExpressionPath) => void;
};

type ExpressionMathElementProps = Omit<NodeEditorProps, "expression" | "root" | "debugOperator"> & {
  expression: IFieldDefinition;
  operator?: MatchOperation | null;
};

function MathOperatorSelect({
  value,
  onChange,
}: {
  value: MatchOperation;
  onChange: (operator: MatchOperation) => void;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!isOpen) return;
    const closeOnOutsidePointerDown = (event: PointerEvent) => {
      if (event.target instanceof Node && !containerRef.current?.contains(event.target))
        setIsOpen(false);
    };
    document.addEventListener("pointerdown", closeOnOutsidePointerDown);
    return () => document.removeEventListener("pointerdown", closeOnOutsidePointerDown);
  }, [isOpen]);

  return (
    <div
      className="expression-math-operator-select"
      ref={containerRef}
      onClick={(event) => event.stopPropagation()}
      onKeyDown={(event) => {
        if (event.key !== "Escape") return;
        event.preventDefault();
        event.stopPropagation();
        setIsOpen(false);
        triggerRef.current?.focus();
      }}
    >
      <button
        ref={triggerRef}
        type="button"
        className="expression-math-operator-trigger"
        aria-label={`Expression operator: ${mathOperatorLabels[value]}`}
        title={mathOperatorLabels[value]}
        aria-expanded={isOpen}
        aria-haspopup="listbox"
        onClick={() => setIsOpen((open) => !open)}
      >
        <Icon name={mathOperatorIcons[value]} size={16} />
      </button>
      {isOpen && (
        <div className="expression-math-operator-menu operator-menu" role="listbox" aria-label="Expression operators">
          {mathOperators.map((operator) => (
            <button
              key={operator}
              type="button"
              className={`operator-option${operator === value ? " operator-option-selected" : ""}`}
              role="option"
              aria-label={mathOperatorLabels[operator]}
              title={mathOperatorLabels[operator]}
              aria-selected={operator === value}
              onClick={() => {
                onChange(operator);
                setIsOpen(false);
                triggerRef.current?.focus();
              }}
            >
              <Icon name={mathOperatorIcons[operator]} size={16} />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

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
  selectionPath,
  selectedPath,
  onSelect,
}: ExpressionMathElementProps) {
  const isMath = expression.kind === "math";
  const operand = isMath ? expression.field : expression;
  const actualOperator = isMath ? (operator === null ? null : expression.oper) : operator;
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
    <div className={`expression-math-element${isMath ? " expression-debug-target expression-debug-math-box" : ""}${operand.kind === "group" && actualOperator ? " expression-math-element-group-operand" : ""}`}>
      <div className="expression-math-operation">
        {actualOperator && (
          <MathOperatorSelect
            value={actualOperator}
            onChange={updateOperator}
          />
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
          selectionPath={selectionPath}
          selectedPath={selectedPath}
          onSelect={onSelect}
        />
      </div>
      {isMath && <ExpressionDebugTooltip label="Math box" model={expression} />}
    </div>
  );
}

export default function NodeEditor({
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
  selectionPath,
  selectedPath,
  onSelect,
}: NodeEditorProps) {
  const isDropPreview = path !== undefined && dragPreview !== undefined && isSameExpressionPath(path, dragPreview);
  const isSelected = selectionPath !== undefined
    && selectedPath !== undefined
    && isSameExpressionPath(selectionPath, selectedPath);
  const selectExpression = (event: ReactMouseEvent<HTMLElement>) => {
    if (selectionPath === undefined) return;
    event.stopPropagation();
    onSelect?.(selectionPath);
  };
  const startSelectedDrag = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!isSelected || !path?.length || !onPointerDragStart) return;
    const target = event.target;
    if (target instanceof Element) {
      if (target.closest("button, input, select, textarea, a, [role='button'], [role='option'], [role='tab'], [contenteditable='true'], .field-builder-value")) return;
      if (target.closest(".expression-selectable") !== event.currentTarget) return;
    }
    onPointerDragStart(event, path);
  };
  if (expression.kind === "field") {
    return (
      <div
        className={`expression-field-drop-target${selectionPath === undefined ? "" : " expression-selectable"}${isSelected ? " expression-selected" : ""}${isDropPreview ? " expression-drop-preview" : ""}${isDropPreview && dragPosition ? ` expression-drop-position-${dragPosition}` : ""}`}
        tabIndex={selectionPath === undefined ? undefined : 0}
        onClick={selectionPath === undefined ? undefined : selectExpression}
        onFocus={(event) => {
          if (
            selectionPath !== undefined
            && (event.target === event.currentTarget
              || (event.target instanceof Element
                && event.target.closest(".field-builder-value, .field-box-menu .operator-trigger")))
          )
            onSelect?.(selectionPath);
        }}
        onPointerDown={startSelectedDrag}
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
            allowFunctionSelection
            editOnDoubleClick
            initialSection="fields"
            expressionBuilder={{
              functionDefinitions,
              functionDefinitionsStatus,
              functionDefinitionsError,
            }}
            debugModel={expression}
            debugOperator={debugOperator}
            onValueSelect={(value) => onChange({ kind: "value", value })}
            onExpressionApply={onChange}
          />
          {isSelected && (
            <ExpressionAddNextItem
              onAddNextItem={onCalculationSelect ?? (() => onChange({
                kind: "group",
                arguments: [
                  expression,
                  { kind: "math", oper: "+", field: { kind: "field", field: "" } },
                ],
              }))}
            />
          )}
          <ExpressionDebugTooltip label="Field builder" model={expression} />
        </div>
      </div>
    );
  }
  if (expression.kind === "group") {
    const groupDebugModel: IFieldDefinition = expression.operator === undefined && debugOperator != null
      ? { ...expression, operator: debugOperator }
      : expression;
    const addNextItem = onCalculationSelect ?? (() => onChange({
      ...expression,
      arguments: [
        ...expression.arguments,
        { kind: "math", oper: "+", field: { kind: "field", field: "" } },
      ],
    }));
    return (
      <div
        className={`expression-group-list expression-debug-target expression-debug-group-box${root ? " expression-group-root" : " expression-group-nested"}${selectionPath === undefined ? "" : " expression-selectable"}${isSelected ? " expression-selected" : ""}${isDropPreview ? " expression-drop-preview" : ""}`}
        tabIndex={selectionPath === undefined ? undefined : root ? -1 : 0}
        onClick={selectionPath === undefined ? undefined : (event) => {
          selectExpression(event);
          const target = event.target;
          if (
            !(target instanceof Element)
            || !target.closest("button, input, select, textarea, a, [role='button'], [role='option'], [role='tab'], [contenteditable='true']")
          )
            event.currentTarget.focus();
        }}
        onFocus={(event) => {
          if (event.target === event.currentTarget && selectionPath !== undefined)
            onSelect?.(selectionPath);
        }}
        onPointerDown={startSelectedDrag}
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
              selectionPath={selectionPath === undefined ? undefined : [...selectionPath, index]}
              selectedPath={selectedPath}
              onSelect={onSelect}
            />
          );
        })}
        {isSelected && !root && <ExpressionAddNextItem onAddNextItem={addNextItem} />}
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
        selectionPath={selectionPath}
        selectedPath={selectedPath}
        onSelect={onSelect}
      />
    );
  }
  if (expression.kind === "value") {
    const constantValue = typeof expression.value === "string" || typeof expression.value === "number"
      ? expression.value
      : String(expression.value ?? "");
    return (
      <div
        className={`expression-debug-target expression-field-drop-target expression-value-node${selectionPath === undefined ? "" : " expression-selectable"}${isSelected ? " expression-selected" : ""}${isDropPreview ? " expression-drop-preview" : ""}${isDropPreview && dragPosition ? ` expression-drop-position-${dragPosition}` : ""}`}
        tabIndex={selectionPath === undefined ? undefined : 0}
        onClick={selectionPath === undefined ? undefined : selectExpression}
        onFocus={(event) => {
          if (event.target === event.currentTarget && selectionPath !== undefined)
            onSelect?.(selectionPath);
        }}
        onPointerDown={startSelectedDrag}
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
          allowFunctionSelection
          editOnDoubleClick
          expressionBuilder={{
            functionDefinitions,
            functionDefinitionsStatus,
            functionDefinitionsError,
          }}
          debugModel={expression}
          onValueSelect={(value) => onChange({ ...expression, value })}
          onExpressionApply={onChange}
        />
        {isSelected && (
          <ExpressionAddNextItem
            onAddNextItem={onCalculationSelect ?? (() => onChange({
              kind: "group",
              arguments: [
                expression,
                { kind: "math", oper: "+", field: { kind: "field", field: "" } },
              ],
            }))}
          />
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
        <ExpressionDebugTooltip label="Field box" model={expression} />
      </div>
    );
  }

  const addNextItem = onCalculationSelect ?? (() => onChange({
    kind: "group",
    arguments: [
      expression,
      { kind: "math", oper: "+", field: { kind: "field", field: "" } },
    ],
  }));

  return (
    <FunctionBuilder
      expression={expression}
      functionDefinitions={functionDefinitions}
      onChange={onChange}
      onRemove={onRemove}
      root={root}
      path={path}
      selectionPath={selectionPath}
      selectedPath={selectedPath}
      onSelect={onSelect}
      onPointerDragStart={onPointerDragStart}
      onAddNextItem={addNextItem}
      renderArgument={(argument, index, onArgumentChange) => {
        const appendMathToArgument = () => {
          const currentArgument = expression.arguments[index];
          if (!currentArgument) return;
          const group = currentArgument.kind === "group"
            ? currentArgument
            : { kind: "group" as const, arguments: [currentArgument] };
          onChange({
            ...expression,
            arguments: expression.arguments.map((item, itemIndex) => itemIndex === index
              ? {
                ...group,
                arguments: [
                  ...group.arguments,
                  { kind: "math" as const, oper: "+" as const, field: { kind: "field" as const, field: "" } },
                ],
              }
              : item),
          });
        };

        return (
          <NodeEditor
            expression={argument}
            entity={entity}
            metadata={metadata}
            functionDefinitions={functionDefinitions}
            functionDefinitionsStatus={functionDefinitionsStatus}
            functionDefinitionsError={functionDefinitionsError}
            onChange={onArgumentChange}
            onCalculationSelect={appendMathToArgument}
            path={path === undefined ? undefined : [...path, index]}
            onPointerDragStart={onPointerDragStart}
            dragPreview={dragPreview}
            dragPosition={dragPosition}
            selectionPath={selectionPath === undefined ? undefined : [...selectionPath, index]}
            selectedPath={selectedPath}
            onSelect={onSelect}
          />
        );
      }}
    />
  );
}
