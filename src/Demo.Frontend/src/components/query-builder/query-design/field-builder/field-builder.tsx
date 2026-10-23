import { useState } from "react";
import FieldBox, { resolveEntity } from "./field-box/field-box";
import { IFieldDefinition, MatchOperation, MetadataEntity } from "../query-builder-slice";
import Icon from "../../../icon/icon";
import ExpressionBuilder from "./expression-builder";
import { FunctionDefinition } from "../../../metadata-builder/function-definition/types";
import ExpressionDebugTooltip from "./expression-debug-tooltip/expression-debug-tooltip";
import "./field-builder.css";

const FIELD_BOX_HEIGHT = 34;

export type FieldBuilderExpressionOptions = {
  functionDefinitions: FunctionDefinition[];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  initialKind?: "group" | "function";
  initialExpression?: IFieldDefinition;
};

type FieldBuilderProps = {
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  value: string;
  onChange: (path: string) => void;
  onRemove?: () => void;
  showPath?: boolean;
  showDataType?: boolean;
  placeholder?: string;
  placeholderIcon?: boolean;
  ariaLabel?: string;
  excludedPaths?: string[];
  autoFocus?: boolean;
  editMode?: boolean;
  onCancel?: () => void;
  allowAggregates?: boolean;
  allowValueSelection?: boolean;
  allowCalculationSelection?: boolean;
  initialSection?: "fields" | "value" | "calculation";
  initialConstantValue?: string | number;
  onValueSelect?: (value: string | number) => void;
  expressionBuilder?: FieldBuilderExpressionOptions;
  debugModel?: IFieldDefinition;
  debugOperator?: MatchOperation | null;
  onCalculationSelect?: () => void;
  onExpressionApply?: (expression: IFieldDefinition) => void;
};

function getFieldDataType(entity: MetadataEntity, metadata: MetadataEntity[], value: string) {
  const segments = value.split(".");
  const lastSegment = segments[segments.length - 1] ?? "";
  const aggregateField = value.match(/^(Count\(\)|Sum|Max|Min|Average)\(([^)]*)\)$/)?.[2]
    ?? lastSegment.match(/^(Count\(\)|Sum|Max|Min|Average)\(([^)]*)\)$/)?.[2];
  if (value.startsWith("Count()") || aggregateField) return "Number";

  const owner = resolveEntity(entity, metadata, segments.slice(0, -1));
  return owner?.fields.find((field) => field.name === lastSegment)?.dataType;
}

export default function FieldBuilder({
  entity,
  metadata,
  value,
  onChange,
  onRemove,
  showPath = false,
  showDataType = false,
  placeholder = "Select field…",
  placeholderIcon = false,
  ariaLabel = "Field",
  excludedPaths = [],
  autoFocus,
  editMode = false,
  onCancel,
  allowAggregates = true,
  allowValueSelection = false,
  allowCalculationSelection = false,
  initialConstantValue,
  initialSection = initialConstantValue !== undefined ? "value" : "fields",
  onValueSelect,
  expressionBuilder,
  debugModel,
  debugOperator,
  onCalculationSelect,
  onExpressionApply,
}: FieldBuilderProps) {
  const [isEditing, setIsEditing] = useState(editMode);
  const [hasIncompleteSelection, setHasIncompleteSelection] = useState(false);
  const cancelEdit = () => {
    setIsEditing(false);
    onCancel?.();
  };
  const renderCalculation = onExpressionApply && expressionBuilder
    ? (closeMenu: () => void) => <ExpressionBuilder
      {...expressionBuilder}
      entity={entity}
      metadata={metadata}
      onApply={(expression) => {
        setIsEditing(false);
        onExpressionApply(expression);
        closeMenu();
      }}
    />
    : undefined;
  const dataType = showDataType ? getFieldDataType(entity, metadata, value) : undefined;
  const removeButton = onRemove && (
    <button
      type="button"
      className="field-builder-remove"
      aria-label={`Remove ${ariaLabel.toLowerCase()}`}
      title={`Remove ${ariaLabel.toLowerCase()}`}
      onClick={onRemove}
    >
      <Icon name="close" size={12} />
    </button>
  );
  const fieldBox = (
    <FieldBox
      entity={entity}
      metadata={metadata}
      value={value}
      onIncompleteChange={setHasIncompleteSelection}
      clearOnSelect
      autoFocus={autoFocus ?? (Boolean(value) || initialConstantValue !== undefined)}
      showPath={showPath}
      placeholder={initialConstantValue === undefined ? placeholder : String(initialConstantValue)}
      placeholderIcon={placeholderIcon}
      ariaLabel={ariaLabel}
      excludedPaths={excludedPaths}
      allowAggregates={allowAggregates}
      allowValueSelection={allowValueSelection}
      allowCalculationSelection={allowCalculationSelection}
      initialSection={initialSection}
      initialConstantValue={initialConstantValue}
      onCalculationSelect={onCalculationSelect ? () => {
        setIsEditing(false);
        onCalculationSelect();
      } : undefined}
      onEscape={cancelEdit}
      onValueSelect={(constantValue) => {
        setIsEditing(false);
        onValueSelect?.(constantValue);
      }}
      renderCalculation={renderCalculation}
      onChange={(path) => {
        setIsEditing(false);
        onChange(path);
      }}
    />
  );
  const fieldBoxDebugModel: IFieldDefinition = debugOperator
    ? { kind: "math", oper: debugOperator, field: { kind: "field", field: value } }
    : debugModel ?? { kind: "field", field: value };

  if (!value || editMode || isEditing) {
    return (
      <div
        className={`field-builder-editing${hasIncompleteSelection ? " field-builder-incomplete" : ""}`}
        autoFocus
        tabIndex={-1}
        onKeyDown={(event) => {
          if (event.key === "Escape") {
            event.preventDefault();
            cancelEdit();
          }
        }}
        onBlurCapture={(event) => {
          const nextTarget = event.relatedTarget;
          if (nextTarget instanceof Element && nextTarget.closest(".field-box-menu-popup")) return;
          if (!nextTarget || !(nextTarget instanceof Node) || !event.currentTarget.contains(nextTarget)) {
            cancelEdit();
          }
        }
      }
      >
        {debugModel
          ? <div className="expression-debug-target expression-debug-field-box">
            {fieldBox}
            <ExpressionDebugTooltip label="Field box" model={fieldBoxDebugModel} />
          </div>
          : fieldBox}
        {dataType && <span className="field-builder-type">{dataType}</span>}
        {removeButton}
      </div>
    );
  }

  const displayValue = showPath ? value : value.split(".").pop() ?? value;

  return (
    <div className="field-builder-selected" style={{ height: FIELD_BOX_HEIGHT }}>
      <span
        className="field-builder-value"
        aria-label={`Selected ${ariaLabel.toLowerCase()}: ${value}`}
        onClick={() => setIsEditing(true)}
        role="button"
        tabIndex={0}
        onKeyDown={(event) => {
          if (event.key === "Enter" || event.key === " ") setIsEditing(true);
        }}
      >
        {displayValue}
      </span>
      {dataType && <span className="field-builder-type">{dataType}</span>}
      {removeButton}
    </div>
  );
}
