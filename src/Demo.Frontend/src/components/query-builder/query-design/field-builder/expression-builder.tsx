import { useEffect, useMemo, useRef, useState } from "react";
import { MetadataEntity, QueryExpression } from "../query-builder-slice";
import FieldBuilder from "./field-builder";
import Icon from "../../../icon/icon";
import { FunctionDefinition } from "../../../metadata-builder/function-definition/types";
import "./expression-builder.css";

type ExpressionBuilderProps = {
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  functionDefinitions: FunctionDefinition[];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  initialKind?: "binary" | "function";
  initialExpression?: QueryExpression;
  onApply: (expression: QueryExpression) => void;
  onCancel: () => void;
};

type ExpressionKind = QueryExpression["kind"];

type ExpressionDropdownOption<Value extends string> = {
  value: Value;
  label: string;
};

function ExpressionDropdown<Value extends string>({
  value,
  options,
  onChange,
  ariaLabel,
  triggerClassName = "",
  wrapperClassName = "",
  searchable = false,
  status = "succeeded",
  error = "",
}: {
  value: Value;
  options: ExpressionDropdownOption<Value>[];
  onChange: (value: Value) => void;
  ariaLabel: string;
  triggerClassName?: string;
  wrapperClassName?: string;
  searchable?: boolean;
  status?: string;
  error?: string;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [search, setSearch] = useState("");
  const menuRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!isOpen) return;

    const closeOnPointerDown = (event: PointerEvent) => {
      if (event.target instanceof Node && !menuRef.current?.contains(event.target)) {
        setIsOpen(false);
      }
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsOpen(false);
        triggerRef.current?.focus();
      }
    };
    document.addEventListener("pointerdown", closeOnPointerDown);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("pointerdown", closeOnPointerDown);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) setSearch("");
  }, [isOpen]);

  const selectedOption = options.find((option) => option.value === value);
  const visibleOptions = options.filter((option) =>
    option.label.toLowerCase().includes(search.trim().toLowerCase()));

  return (
    <div className={`expression-dropdown ${wrapperClassName}`.trim()} ref={menuRef}>
      <button
        type="button"
        className={`expression-dropdown-trigger ${triggerClassName}`.trim()}
        aria-label={ariaLabel}
        aria-haspopup={searchable ? "listbox" : "menu"}
        aria-expanded={isOpen}
        ref={triggerRef}
        onClick={() => setIsOpen((open) => !open)}
      >
        <span>{selectedOption?.label ?? value}</span>
      </button>
      {isOpen && (
        <div
          className={`expression-dropdown-options${searchable ? " expression-dropdown-searchable" : ""}`}
          role={searchable ? "listbox" : "menu"}
          aria-label={ariaLabel}
        >
          {searchable && (
            <input
              type="search"
              className="expression-function-search"
              role="searchbox"
              aria-label={`Search ${ariaLabel.toLowerCase()}s`}
              placeholder={`Search ${ariaLabel.toLowerCase()}s…`}
              autoFocus
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          )}
          {searchable && status !== "succeeded" && status !== "failed" && (
            <span className="expression-function-message">Loading function definitions…</span>
          )}
          {searchable && status === "failed" && (
            <span className="expression-function-message error" role="alert">
              Unable to load function definitions: {error}
            </span>
          )}
          {(!searchable || status === "succeeded") && visibleOptions.map((option) => (
            <button
              key={option.value}
              type="button"
              role={searchable ? "option" : "menuitemradio"}
              aria-checked={searchable ? undefined : option.value === value}
              aria-selected={searchable ? option.value === value : undefined}
              className={`expression-change-option${option.value === value ? " selected" : ""}`}
              onClick={() => {
                onChange(option.value);
                setIsOpen(false);
              }}
            >
              {option.label}
            </button>
          ))}
          {searchable && status === "succeeded" && visibleOptions.length === 0 && (
            <span className="expression-function-message">No matching functions.</span>
          )}
        </div>
      )}
    </div>
  );
}

function createExpression(kind: ExpressionKind): QueryExpression {
  switch (kind) {
    case "field":
      return { kind, field: "" };
    case "value":
      return { kind, value: 0 };
    case "binary":
      return { kind, operator: "-", arguments: [{ kind: "field", field: "" }, { kind: "field", field: "" }] };
    case "function":
      return { kind, function: "", arguments: [] };
  }
}

function createFunctionExpression(definition?: FunctionDefinition): QueryExpression {
  return {
    kind: "function",
    function: definition?.name ?? "",
    arguments: (definition?.arguments ?? []).map((argument, index) => {
      if (typeof argument.defaultValue === "number" && Number.isFinite(argument.defaultValue)) {
        return { kind: "value", value: argument.defaultValue };
      }
      if (typeof argument.defaultValue === "string") {
        return { kind: "value", value: argument.defaultValue };
      }
      if (index === 1 && definition?.name === "ROUND") {
        return { kind: "value", value: 2 };
      }
      if (index === 1 && definition?.name === "NULLIF") {
        return { kind: "value", value: 0 };
      }
      return { kind: "field", field: "" };
    }),
  };
}

type Calculation = Extract<QueryExpression, { kind: "binary" }>;
type CalculationStep = { operator: Calculation["operator"]; expression: QueryExpression };

function flattenCalculation(expression: Calculation, groupedStart?: QueryExpression): { first: QueryExpression; steps: CalculationStep[] } {
  if (expression.arguments[0].kind !== "binary" || expression.arguments[0] === groupedStart) {
    return {
      first: expression.arguments[0],
      steps: [{ operator: expression.operator, expression: expression.arguments[1] }],
    };
  }

  const left = flattenCalculation(expression.arguments[0], groupedStart);
  return {
    first: left.first,
    steps: [...left.steps, { operator: expression.operator, expression: expression.arguments[1] }],
  };
}

function buildCalculation(first: QueryExpression, steps: CalculationStep[]): Calculation {
  if (!steps.length) {
    return { kind: "binary", operator: "+", arguments: [first, { kind: "field", field: "" }] };
  }

  return steps.slice(1).reduce<Calculation>((result, step) => ({
    kind: "binary",
    operator: step.operator,
    arguments: [result, step.expression],
  }), {
    kind: "binary",
    operator: steps[0].operator,
    arguments: [first, steps[0].expression],
  });
}

function isComplete(expression: QueryExpression): boolean {
  switch (expression.kind) {
    case "field":
      return Boolean(expression.field.trim());
    case "value":
      return typeof expression.value === "string" || Number.isFinite(expression.value);
    case "binary":
      return expression.arguments.every(isComplete);
    case "function":
      return Boolean(expression.function.trim()) && expression.arguments.every(isComplete);
  }
}

export function formatExpression(expression: QueryExpression): string {
  switch (expression.kind) {
    case "field":
      return expression.field || "Select field";
    case "value":
      return String(expression.value);
    case "binary":
      return `(${formatExpression(expression.arguments[0])} ${expression.operator} ${formatExpression(expression.arguments[1])})`;
    case "function":
      return `${expression.function}(${expression.arguments.map(formatExpression).join(", ")})`;
  }
}

function ExpressionNodeEditor({
  expression,
  entity,
  metadata,
  functionDefinitions,
  functionDefinitionsStatus,
  functionDefinitionsError,
  onChange,
  onRemove,
  removeLabel = "Remove expression",
  onUngroup,
  depth = 0,
  compact = false,
}: {
  expression: QueryExpression;
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  functionDefinitions: FunctionDefinition[];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  onChange: (expression: QueryExpression) => void;
  onRemove?: () => void;
  removeLabel?: string;
  onUngroup?: (first: QueryExpression, steps: CalculationStep[]) => void;
  depth?: number;
  compact?: boolean;
}) {
  const [groupedStart, setGroupedStart] = useState<QueryExpression | null>(null);
  const [calculationCollapsed, setCalculationCollapsed] = useState(false);
  const [functionCollapsed, setFunctionCollapsed] = useState(false);
  const replaceKind = (kind: ExpressionKind) => {
    setGroupedStart(null);
    onChange(kind === "function"
      ? createFunctionExpression(functionDefinitions[0])
      : createExpression(kind));
  };
  const [isTypeMenuOpen, setIsTypeMenuOpen] = useState(false);
  const [isRemoveMenuOpen, setIsRemoveMenuOpen] = useState(false);
  const typeMenuRef = useRef<HTMLDivElement>(null);
  const typeMenuLinkRef = useRef<HTMLAnchorElement>(null);
  const removeMenuRef = useRef<HTMLDivElement>(null);
  const removeMenuButtonRef = useRef<HTMLButtonElement>(null);
  const calculation = expression.kind === "binary"
    ? flattenCalculation(expression, groupedStart ?? undefined)
    : undefined;
  const updateCalculation = (first: QueryExpression, steps: CalculationStep[]) => {
    onChange(buildCalculation(first, steps));
  };
  const expressionKindOptions: Array<{ kind: ExpressionKind; label: string }> = [
    { kind: "field", label: "Field" },
    { kind: "value", label: "Value" },
    { kind: "binary", label: "Calculation" },
    { kind: "function", label: "SQL function" },
  ];
  useEffect(() => {
    if (!isTypeMenuOpen && !isRemoveMenuOpen) return;

    const closeOnPointerDown = (event: PointerEvent) => {
      if (event.target instanceof Node
        && !typeMenuRef.current?.contains(event.target)
        && !removeMenuRef.current?.contains(event.target)) {
        setIsTypeMenuOpen(false);
        setIsRemoveMenuOpen(false);
      }
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsTypeMenuOpen(false);
        setIsRemoveMenuOpen(false);
        if (isRemoveMenuOpen) removeMenuButtonRef.current?.focus();
        else typeMenuLinkRef.current?.focus();
      }
    };
    document.addEventListener("pointerdown", closeOnPointerDown);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("pointerdown", closeOnPointerDown);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [isTypeMenuOpen, isRemoveMenuOpen]);
  const typeSelector = (
    <ExpressionDropdown
      value={expression.kind}
      options={expressionKindOptions.map(({ kind, label }) => ({ value: kind, label }))}
      onChange={replaceKind}
      ariaLabel="Expression node type"
      triggerClassName="expression-type-dropdown"
    />
  );
  const rowAction = onRemove && (
    <button
      type="button"
      className={`expression-remove-row${expression.kind === "binary" && onUngroup ? " ungroup" : ""}`}
      aria-label={expression.kind === "binary" && onUngroup ? `Ungroup ${removeLabel.replace(/^Remove /, "")}` : removeLabel}
      onClick={() => {
        if (expression.kind === "binary" && calculation && onUngroup) {
          onUngroup(calculation.first, calculation.steps);
          return;
        }
        onRemove();
      }}
    >
      <Icon name={expression.kind === "binary" && onUngroup ? "ungroup" : "close"} size={12} />
    </button>
  );
  const advancedOptions = (
    <div className="expression-change-menu" ref={typeMenuRef}>
      <a
        href="#"
        className="expression-change-link"
        aria-label="Change expression type"
        aria-haspopup="menu"
        aria-expanded={isTypeMenuOpen}
        ref={typeMenuLinkRef}
        onClick={(event) => {
          event.preventDefault();
          setIsTypeMenuOpen((isOpen) => !isOpen);
        }}
      >
        <Icon name="edit" size={14} />
      </a>
      {isTypeMenuOpen && (
        <div className="expression-change-options" role="menu" aria-label="Change expression type">
          {expressionKindOptions.map(({ kind, label }) => (
            <button
              key={kind}
              type="button"
              role="menuitemradio"
              aria-checked={expression.kind === kind}
              className={`expression-change-option${expression.kind === kind ? " selected" : ""}`}
              onClick={() => {
                replaceKind(kind);
                setIsTypeMenuOpen(false);
              }}
            >
              {label}
            </button>
          ))}
        </div>
      )}
    </div>
  );

  return (
    <div className={`expression-node${depth ? " nested" : " root"}${expression.kind === "function" ? " expression-function-node" : ""}`}>
      {(!compact || expression.kind === "function") && (
        <div className="expression-node-toolbar">
          {expression.kind === "function" && (
            <button
              type="button"
              className="expression-collapse-toggle"
              aria-label={`${functionCollapsed ? "Expand" : "Collapse"} ${expression.function || "function"}`}
              aria-expanded={!functionCollapsed}
              title={`${functionCollapsed ? "Expand" : "Collapse"} ${expression.function || "function"}`}
              onClick={() => setFunctionCollapsed((collapsed) => !collapsed)}
            >
              <Icon name={functionCollapsed ? "chevron-right" : "chevron-down"} size={12} />
            </button>
          )}
          {!compact && (
            <label className="expression-type">
              {typeSelector}
            </label>
          )}
          {expression.kind === "function" && (
            <label>
              <ExpressionDropdown
                value={expression.function}
                options={functionDefinitions.map((definition) => ({ value: definition.name, label: definition.name }))}
                onChange={(functionName) => {
                  const definition = functionDefinitions.find((item) => item.name === functionName);
                  if (definition) onChange(createFunctionExpression(definition));
                }}
                ariaLabel="Expression function"
                wrapperClassName="expression-dropdown-plain expression-function-dropdown"
                searchable
                status={functionDefinitionsStatus}
                error={functionDefinitionsError}
              />
            </label>
          )}
          {compact && expression.kind === "function" && advancedOptions}
          {expression.kind === "function" && rowAction}
        </div>
      )}
      {expression.kind === "binary" && calculation && (
        <div className={`filter-group filter-group-depth-${Math.min(depth + 1, 3)} expression-calculation${depth === 0 ? " expression-calculation-root" : ""}`}>
          <div className="filter-group-header expression-calculation-header">
            <button
              type="button"
              className="expression-collapse-toggle"
              aria-label={calculationCollapsed ? "Expand calculation" : "Collapse calculation"}
              aria-expanded={!calculationCollapsed}
              title={calculationCollapsed ? "Expand calculation" : "Collapse calculation"}
              onClick={() => setCalculationCollapsed((collapsed) => !collapsed)}
            >
              <Icon name={calculationCollapsed ? "chevron-right" : "chevron-down"} size={12} />
            </button>
            <span className="filter-group-title">CALCULATION</span>
            {compact && advancedOptions}
            {rowAction}
            <div className="expression-remove-menu" ref={removeMenuRef}>
              <button
                type="button"
                className="expression-remove-group"
                aria-label="Calculation group actions"
                aria-haspopup="menu"
                aria-expanded={isRemoveMenuOpen}
                ref={removeMenuButtonRef}
                onClick={() => setIsRemoveMenuOpen((isOpen) => !isOpen)}
              >
                <Icon name="close" size={12} />
              </button>
              {isRemoveMenuOpen && (
                <div className="expression-remove-options" role="menu" aria-label="Calculation group actions">
                  <button
                    type="button"
                    role="menuitem"
                    className="expression-change-option"
                    onClick={() => {
                      setIsRemoveMenuOpen(false);
                      setGroupedStart(null);
                      onChange(calculation.first);
                    }}
                  >
                    Remove group
                  </button>
                  {onUngroup && (
                    <button
                      type="button"
                      role="menuitem"
                      className="expression-change-option"
                      onClick={() => {
                        setIsRemoveMenuOpen(false);
                        onUngroup(calculation.first, calculation.steps);
                      }}
                    >
                      Ungroup
                    </button>
                  )}
                </div>
              )}
            </div>
          </div>
          {!calculationCollapsed && (
          <>
          <div className="expression-terms">
            <div className="expression-term expression-term-first">
              <ExpressionNodeEditor
                expression={calculation.first}
                entity={entity}
                metadata={metadata}
                functionDefinitions={functionDefinitions}
                functionDefinitionsStatus={functionDefinitionsStatus}
                functionDefinitionsError={functionDefinitionsError}
                depth={depth + 1}
                compact
                onUngroup={(first, steps) => {
                  setGroupedStart(first.kind === "binary" ? first : null);
                  updateCalculation(first, [...steps, ...calculation.steps]);
                }}
                removeLabel="Remove calculation starting value"
                onRemove={() => {
                  const [nextFirst, ...steps] = calculation.steps;
                  if (steps.length) {
                    setGroupedStart(nextFirst.expression.kind === "binary" ? nextFirst.expression : null);
                    updateCalculation(nextFirst.expression, steps);
                  } else {
                    setGroupedStart(null);
                    onChange(nextFirst.expression);
                  }
                }}
                onChange={(replacement) => {
                  setGroupedStart(replacement.kind === "binary" ? replacement : null);
                  updateCalculation(replacement, calculation.steps);
                }}
              />
            </div>
            {calculation.steps.map((step, index) => (
              <div className="expression-term" key={index}>
                <label className="expression-term-operator">
                  <ExpressionDropdown
                    value={step.operator}
                    options={[
                      { value: "+", label: "Add (+)" },
                      { value: "-", label: "Subtract (-)" },
                      { value: "*", label: "Multiply (*)" },
                      { value: "/", label: "Divide (/)" },
                    ]}
                    ariaLabel={`Calculation operator ${index + 1}`}
                    wrapperClassName="expression-dropdown-plain"
                    onChange={(operator) => {
                      const steps = [...calculation.steps];
                      steps[index] = { ...step, operator };
                      updateCalculation(calculation.first, steps);
                    }}
                  />
                </label>
                <ExpressionNodeEditor
                  expression={step.expression}
                  entity={entity}
                  metadata={metadata}
                  functionDefinitions={functionDefinitions}
                  functionDefinitionsStatus={functionDefinitionsStatus}
                  functionDefinitionsError={functionDefinitionsError}
                  depth={depth + 1}
                  compact
                  onUngroup={(first, childSteps) => {
                    const steps = [
                      ...calculation.steps.slice(0, index),
                      { operator: step.operator, expression: first },
                      ...childSteps,
                      ...calculation.steps.slice(index + 1),
                    ];
                    updateCalculation(calculation.first, steps);
                  }}
                  removeLabel={`Remove calculation term ${index + 2}`}
                  onRemove={() => {
                    const steps = calculation.steps.filter((_, stepIndex) => stepIndex !== index);
                    if (!steps.length) {
                      setGroupedStart(null);
                      onChange(calculation.first);
                      return;
                    }
                    updateCalculation(calculation.first, steps);
                  }}
                  onChange={(replacement) => {
                    const steps = [...calculation.steps];
                    steps[index] = { ...step, expression: replacement };
                    updateCalculation(calculation.first, steps);
                  }}
                />
              </div>
            ))}
          </div>
          <a
            href="#"
            className="expression-add-term"
            onClick={(event) => {
              event.preventDefault();
              updateCalculation(calculation.first, [
                ...calculation.steps,
                { operator: "+", expression: { kind: "field", field: "" } },
              ]);
            }}
          >
            + More
          </a>
          </>
          )}
        </div>
      )}
      {expression.kind === "field" && (
        <div className="expression-field-control">
          <FieldBuilder
            entity={entity}
            metadata={metadata}
            value={expression.field}
            showPath
            ariaLabel="Calculation field"
            placeholder="Choose field"
            allowAggregates={false}
            onChange={(field) => onChange({ ...expression, field })}
          />
          {compact && advancedOptions}
          {rowAction}
        </div>
      )}
      {expression.kind === "value" && (
        <div className="expression-value-control">
          <input
            className="expression-number"
            type={typeof expression.value === "number" ? "number" : "text"}
            step={typeof expression.value === "number" ? "any" : undefined}
            value={typeof expression.value === "number" && !Number.isFinite(expression.value) ? "" : expression.value}
            aria-label={typeof expression.value === "number" ? "Expression number" : "Expression text value"}
            onChange={(event) => onChange({
              ...expression,
              value: typeof expression.value === "number"
                ? event.target.value === "" ? Number.NaN : Number(event.target.value)
                : event.target.value,
            })}
          />
          {compact && advancedOptions}
          {rowAction}
        </div>
      )}
      {expression.kind === "function" && !functionCollapsed && (
        <div className="expression-arguments">
          {expression.arguments.map((argument, index) => (
            <div className="expression-argument" key={index}>
              <ExpressionNodeEditor
                expression={argument}
                entity={entity}
                metadata={metadata}
                functionDefinitions={functionDefinitions}
                functionDefinitionsStatus={functionDefinitionsStatus}
                functionDefinitionsError={functionDefinitionsError}
                depth={depth + 1}
                compact
                onChange={(replacement) => {
                  const arguments_ = [...expression.arguments];
                  arguments_[index] = replacement;
                  onChange({ ...expression, arguments: arguments_ });
                }}
              />
            </div>
          ))}
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
  initialKind = "binary",
  initialExpression,
  onApply,
  onCancel,
}: ExpressionBuilderProps) {
  const [expression, setExpression] = useState<QueryExpression>(initialExpression
    ?? (initialKind === "function"
      ? createFunctionExpression(functionDefinitions[0])
      : createExpression("binary")));
  const complete = isComplete(expression);
  const preview = useMemo(() => formatExpression(expression), [expression]);

  return (
      <section className="expression-builder">
        <ExpressionNodeEditor
          expression={expression}
          entity={entity}
          metadata={metadata}
          functionDefinitions={functionDefinitions}
          functionDefinitionsStatus={functionDefinitionsStatus}
          functionDefinitionsError={functionDefinitionsError}
          compact
          onChange={setExpression}
        />
        <div className="expression-preview">
          <span className="expression-label">Preview</span>
          <code>{preview}</code>
        </div>
        <footer className="expression-builder-actions">
          <button type="button" className="expression-cancel" onClick={onCancel}>Cancel</button>
          <button
            type="button"
            className="expression-apply"
            disabled={!complete}
            onClick={() => onApply(expression)}
          >
            Apply
          </button>
        </footer>
      </section>
  );
}
