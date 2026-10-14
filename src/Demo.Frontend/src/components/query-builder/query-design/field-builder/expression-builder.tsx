import { useMemo, useState } from "react";
import { MetadataEntity, QueryExpression } from "../query-builder-slice";
import FieldBuilder from "./field-builder";
import { resolveEntity } from "./field-box/field-box";
import "./expression-builder.css";

type ExpressionBuilderProps = {
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  initialExpression?: QueryExpression;
  initialAlias?: string;
  existingAliases?: string[];
  onApply: (expression: QueryExpression, alias: string) => void;
  onCancel: () => void;
};

type ExpressionKind = QueryExpression["kind"];

function createExpression(kind: ExpressionKind): QueryExpression {
  switch (kind) {
    case "field":
      return { kind, field: "" };
    case "value":
      return { kind, value: 0 };
    case "binary":
      return { kind, operator: "-", arguments: [{ kind: "field", field: "" }, { kind: "field", field: "" }] };
    case "function":
      return { kind, function: "SUM", arguments: [{ kind: "field", field: "" }] };
  }
}

function createFunctionExpression(name: "SUM" | "ROUND" | "NULLIF"): QueryExpression {
  const arguments_: QueryExpression[] = name === "SUM"
    ? [{ kind: "field", field: "" }]
    : name === "ROUND"
      ? [{ kind: "field", field: "" }, { kind: "value", value: 2 }]
      : [{ kind: "field", field: "" }, { kind: "value", value: 0 }];
  return { kind: "function", function: name, arguments: arguments_ };
}

function isComplete(expression: QueryExpression): boolean {
  switch (expression.kind) {
    case "field":
      return Boolean(expression.field.trim());
    case "value":
      return Number.isFinite(expression.value);
    case "binary":
    case "function":
      return expression.arguments.every(isComplete);
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

function grossMarginTemplate(): QueryExpression {
  const lineTotal = (): QueryExpression => ({ kind: "field", field: "InvoiceItems.LineTotal" });
  const sumLineTotal = (): QueryExpression => ({ kind: "function", function: "SUM", arguments: [lineTotal()] });
  const grossProfit = (): QueryExpression => ({
    kind: "function",
    function: "SUM",
    arguments: [{
      kind: "binary",
      operator: "-",
      arguments: [
        lineTotal(),
        {
          kind: "binary",
          operator: "*",
          arguments: [
            { kind: "field", field: "InvoiceItems.Quantity" },
            { kind: "field", field: "CostPrice" },
          ],
        },
      ],
    }],
  });
  return {
    kind: "function",
    function: "ROUND",
    arguments: [
      {
        kind: "binary",
        operator: "/",
        arguments: [
          {
            kind: "binary",
            operator: "*",
            arguments: [grossProfit(), { kind: "value", value: 100 }],
          },
          {
            kind: "function",
            function: "NULLIF",
            arguments: [sumLineTotal(), { kind: "value", value: 0 }],
          },
        ],
      },
      { kind: "value", value: 2 },
    ],
  };
}

function ExpressionNodeEditor({
  expression,
  entity,
  metadata,
  onChange,
  depth = 0,
}: {
  expression: QueryExpression;
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  onChange: (expression: QueryExpression) => void;
  depth?: number;
}) {
  const replaceKind = (kind: ExpressionKind) => onChange(createExpression(kind));

  return (
    <div className={`expression-node${depth ? " nested" : ""}`}>
      <div className="expression-node-toolbar">
        <label>
          <span className="expression-label">Type</span>
          <select aria-label="Expression node type" value={expression.kind} onChange={(event) => replaceKind(event.target.value as ExpressionKind)}>
            <option value="field">Field</option>
            <option value="value">Number</option>
            <option value="binary">Calculation</option>
            <option value="function">Function</option>
          </select>
        </label>
        {expression.kind === "binary" && (
          <label>
            <span className="expression-label">Operator</span>
            <select
              aria-label="Calculation operator"
              value={expression.operator}
              onChange={(event) => onChange({ ...expression, operator: event.target.value as "+" | "-" | "*" | "/" })}
            >
              <option value="+">Add (+)</option>
              <option value="-">Subtract (-)</option>
              <option value="*">Multiply (*)</option>
              <option value="/">Divide (/)</option>
            </select>
          </label>
        )}
        {expression.kind === "function" && (
          <label>
            <span className="expression-label">Function</span>
            <select
              aria-label="Expression function"
              value={expression.function}
              onChange={(event) => onChange(createFunctionExpression(event.target.value as "SUM" | "ROUND" | "NULLIF"))}
            >
              <option value="SUM">SUM</option>
              <option value="ROUND">ROUND</option>
              <option value="NULLIF">NULLIF</option>
            </select>
          </label>
        )}
      </div>
      {expression.kind === "field" && (
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
      )}
      {expression.kind === "value" && (
        <label className="expression-number">
          <span className="expression-label">Number</span>
          <input
            type="number"
            step="any"
            value={Number.isFinite(expression.value) ? expression.value : ""}
            aria-label="Expression number"
            onChange={(event) => onChange({ ...expression, value: event.target.value === "" ? Number.NaN : Number(event.target.value) })}
          />
        </label>
      )}
      {(expression.kind === "binary" || expression.kind === "function") && (
        <div className="expression-arguments">
          {expression.arguments.map((argument, index) => (
            <div className="expression-argument" key={index}>
              {expression.kind === "binary" && <span className="expression-argument-label">{index === 0 ? "Left" : "Right"}</span>}
              {expression.kind === "function" && <span className="expression-argument-label">Argument {index + 1}</span>}
              <ExpressionNodeEditor
                expression={argument}
                entity={entity}
                metadata={metadata}
                depth={depth + 1}
                onChange={(replacement) => {
                  const arguments_ = [...expression.arguments];
                  arguments_[index] = replacement;
                  onChange({ ...expression, arguments: arguments_ } as QueryExpression);
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
  initialExpression,
  initialAlias,
  existingAliases = [],
  onApply,
  onCancel,
}: ExpressionBuilderProps) {
  const [expression, setExpression] = useState<QueryExpression>(initialExpression ?? createExpression("function"));
  const [alias, setAlias] = useState(initialAlias ?? "CalculatedValue");
  const duplicateAlias = existingAliases.some((existing) => existing.toLowerCase() === alias.trim().toLowerCase());
  const complete = isComplete(expression) && alias.trim().length > 0 && !duplicateAlias;
  const preview = useMemo(() => formatExpression(expression), [expression]);
  const hasGrossMarginFields = Boolean(
    resolveEntity(entity, metadata, ["InvoiceItems"])?.fields.some((field) => field.name === "LineTotal")
    && resolveEntity(entity, metadata, ["InvoiceItems"])?.fields.some((field) => field.name === "Quantity")
    && entity.fields.some((field) => field.name === "CostPrice"),
  );

  return (
    <div className="expression-builder-overlay" role="presentation" onMouseDown={(event) => {
      if (event.target === event.currentTarget) onCancel();
    }}>
      <section className="expression-builder" role="dialog" aria-modal="true" aria-labelledby="expression-builder-title">
        <header className="expression-builder-header">
          <div>
            <span className="section-kicker">CALCULATED PROJECTION</span>
            <h2 id="expression-builder-title">Build an expression</h2>
          </div>
          <button type="button" className="expression-close" aria-label="Close expression builder" onClick={onCancel}>×</button>
        </header>
        <label className="expression-alias">
          <span>Output name</span>
          <input value={alias} onChange={(event) => setAlias(event.target.value)} aria-label="Calculated column name" />
        </label>
        {duplicateAlias && <span className="expression-error" role="alert">That output name is already in use.</span>}
        {hasGrossMarginFields && (
          <button
            type="button"
            className="expression-template"
            onClick={() => {
              setExpression(grossMarginTemplate());
              setAlias("MarginPercent");
            }}
          >
            Use gross-margin percentage template
          </button>
        )}
        <ExpressionNodeEditor expression={expression} entity={entity} metadata={metadata} onChange={setExpression} />
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
            onClick={() => onApply(expression, alias.trim())}
          >
            Add calculated field
          </button>
        </footer>
      </section>
    </div>
  );
}
