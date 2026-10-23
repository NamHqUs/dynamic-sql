import { useEffect, useRef, useState, type ReactNode } from "react";
import type { IFieldDefinition } from "../../query-builder-slice";
import type { FunctionDefinition } from "../../../../metadata-builder/function-definition/types";
import Icon from "../../../../icon/icon";
import { updateFunction } from "../expression-builder/expression-model";
import { isSameExpressionPath, type ExpressionPath } from "../expression-builder/expression-model";
import "./function-builder.css";

type FunctionBuilderProps = {
  expression: Extract<IFieldDefinition, { kind: "function" }>;
  functionDefinitions: FunctionDefinition[];
  onChange: (expression: IFieldDefinition) => void;
  onRemove?: () => void;
  root?: boolean;
  path?: ExpressionPath;
  selectionPath?: ExpressionPath;
  selectedPath?: ExpressionPath;
  onSelect?: (path: ExpressionPath) => void;
  renderArgument: (
    argument: IFieldDefinition,
    index: number,
    onChange: (expression: IFieldDefinition) => void,
  ) => ReactNode;
};

type FunctionChoice = {
  name: string;
  label: string;
  syntax: string;
};

function FunctionSelect({
  value,
  choices,
  onChange,
}: {
  value: string;
  choices: FunctionChoice[];
  onChange: (value: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const [searchTerm, setSearchTerm] = useState("");
  const containerRef = useRef<HTMLDivElement>(null);
  const selectedChoice = choices.find((choice) => choice.name === value) ?? choices[0];
  const filteredChoices = choices.filter((choice) =>
    choice.label.toLowerCase().includes(searchTerm.trim().toLowerCase()),
  );

  useEffect(() => {
    if (!open) return;

    const closeOnOutsideClick = (event: MouseEvent) => {
      if (event.target instanceof Node && !containerRef.current?.contains(event.target)) setOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };

    document.addEventListener("mousedown", closeOnOutsideClick);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("mousedown", closeOnOutsideClick);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [open]);

  return (
    <div className="expression-function-select" ref={containerRef}>
      <button
        type="button"
        className="operator-trigger"
        aria-label="Function"
        aria-expanded={open}
        aria-haspopup="listbox"
        title={selectedChoice?.syntax}
        onClick={() => setOpen((isOpen) => !isOpen)}
      >
        {selectedChoice?.label ?? "Select function…"}
      </button>
      {open && (
        <div className="expression-function-menu operator-menu" role="group" aria-label="Function options">
          <input
            type="search"
            className="expression-function-search"
            aria-label="Search functions"
            placeholder="Search functions..."
            value={searchTerm}
            autoFocus
            onChange={(event) => setSearchTerm(event.target.value)}
          />
          <div className="expression-function-options" role="listbox" aria-label="Functions">
            {filteredChoices.length > 0 ? filteredChoices.map((choice) => (
              <button
                key={choice.name}
                type="button"
                className={`operator-option${choice.name === value ? " operator-option-selected" : ""}`}
                role="option"
                aria-selected={choice.name === value}
                title={choice.syntax}
                onClick={() => {
                  onChange(choice.name);
                  setSearchTerm("");
                  setOpen(false);
                }}
              >
                {choice.label}
              </button>
            )) : (
              <span className="expression-function-no-results" role="status">No functions found.</span>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

export default function FunctionBuilder({
  expression,
  functionDefinitions,
  onChange,
  onRemove,
  root = false,
  path,
  selectionPath,
  selectedPath,
  onSelect,
  renderArgument,
}: FunctionBuilderProps) {
  const definition = functionDefinitions.find((item) => item.name === expression.function);
  const isSelected = selectionPath !== undefined
    && selectedPath !== undefined
    && isSameExpressionPath(selectionPath, selectedPath);
  const choices = [
    ...(expression.function && !functionDefinitions.some((item) => item.name === expression.function)
      ? [{ name: expression.function, label: expression.function, syntax: `${expression.function}()` }]
      : []),
    ...functionDefinitions.map((item) => ({
      name: item.name,
      label: item.name,
      syntax: `${item.name}(${item.arguments.map((argument) => argument.name).join(", ")})`,
    })),
  ];

  return (
    <div
      className={`expression-node expression-node-function${root ? " expression-node-root" : ""}${selectionPath === undefined ? "" : " expression-selectable"}${isSelected ? " expression-selected" : ""}`}
      tabIndex={selectionPath === undefined ? undefined : 0}
      onClick={selectionPath === undefined ? undefined : (event) => {
        event.stopPropagation();
        onSelect?.(selectionPath);
      }}
      data-expression-kind={expression.kind}
      data-expression-path={path === undefined ? undefined : JSON.stringify(path)}
    >
      <div className="expression-node-heading">
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

      <div className="expression-function-editor">
        <FunctionSelect
          value={expression.function}
          choices={choices}
          onChange={(name) => onChange(updateFunction(expression, name, functionDefinitions))}
        />
        {expression.function && (
          <>
            <span className="expression-function-punctuation" aria-hidden="true">(</span>
            <div className="expression-function-arguments">
              {expression.arguments.map((argument, index) => {
                const argumentDefinition = definition?.arguments[index];
                const argumentLabel = argumentDefinition
                  ? `${argumentDefinition.name} (${argumentDefinition.dataType})`
                  : `Argument ${index + 1}`;
                return (
                  <div
                    className="expression-function-argument"
                    key={`${expression.function}-${index}`}
                    role="group"
                    aria-label={argumentLabel}
                    title={argumentLabel}
                  >
                    {index > 0 && <span className="expression-function-punctuation" aria-hidden="true">,</span>}
                    {renderArgument(argument, index, (next) => onChange({
                      ...expression,
                      arguments: expression.arguments.map((item, itemIndex) => itemIndex === index ? next : item),
                    }))}
                  </div>
                );
              })}
            </div>
            <span className="expression-function-punctuation" aria-hidden="true">)</span>
          </>
        )}
      </div>
    </div>
  );
}
