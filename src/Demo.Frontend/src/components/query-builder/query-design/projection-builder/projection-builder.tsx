import { useEffect, useRef, useState } from "react";
import Icon from "../../../icon/icon";
import { MetadataEntity, QueryColumn, QueryDefinition, QueryExpression } from "../query-builder-slice";
import FieldBuilder, { FieldBuilderExpressionOptions } from "../field-builder/field-builder";
import { formatExpression } from "../field-builder/expression-builder";
import { FunctionDefinition } from "../../../metadata-builder/function-definition/types";
import { HighlightedTextArea } from "../plan-text-section-editor";

type ProjectionBuilderProps = {
  metadata: MetadataEntity[];
  functionDefinitions: FunctionDefinition[];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  definition: QueryDefinition;
  entity: MetadataEntity;
  isReadOnly: boolean;
  onDefinitionChange: (definition: QueryDefinition) => void;
  onFieldDragStart: (field: string | null) => void;
};

function formatProjectionColumn(column: QueryColumn): string {
  const field = column.expression ? formatExpression(column.expression) : column.field;
  return column.alias ? `${field} AS ${column.alias}` : field;
}

function formatProjectionText(columns: QueryColumn[], rows: string[][]): string {
  const columnsByField = new Map(columns.map((column) => [column.field, column]));
  return rows
    .map((row) => row
      .map((field) => {
        const column = columnsByField.get(field);
        return column ? formatProjectionColumn(column) : field;
      })
      .join(", "))
    .join("\n");
}

function splitProjectionText(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let start = 0;
  let depth = 0;
  let quote: string | null = null;

  for (let index = 0; index < text.length; index += 1) {
    const character = text[index];
    if (quote) {
      if (character === quote) {
        if (text[index + 1] === quote) index += 1;
        else quote = null;
      }
      continue;
    }
    if (character === "'" || character === "\"" || character === "`" || character === "[") {
      quote = character === "[" ? "]" : character;
    } else if (character === "(") {
      depth += 1;
    } else if (character === ")") {
      depth = Math.max(0, depth - 1);
    } else if (depth === 0 && (character === "," || character === "\n" || character === "\r")) {
      const item = text.slice(start, index).trim();
      if (item) row.push(item);
      if (character !== ",") {
        if (row.length) rows.push(row);
        row = [];
        if (character === "\r" && text[index + 1] === "\n") index += 1;
      }
      start = index + 1;
    }
  }
  const item = text.slice(start).trim();
  if (item) row.push(item);
  if (row.length) rows.push(row);
  return rows;
}

function getProjectionAlias(column: QueryColumn): string | null {
  return column.alias ?? column.field.match(/\s*\|\s*([A-Za-z_][A-Za-z0-9_]*)\s*$/)?.[1] ?? null;
}

function parseProjectionExpression(text: string, entity: MetadataEntity): QueryExpression | undefined {
  const expressionText = text.replace(/\s*\|\s*[A-Za-z_][A-Za-z0-9_]*\s*$/, "").trim();
  const functionMatch = expressionText.match(/^([A-Za-z_][A-Za-z0-9_]*)\s*\(([\s\S]*)\)$/);
  if (!functionMatch) return undefined;

  const arguments_ = splitProjectionText(functionMatch[2]).flat().map((argument): QueryExpression => {
    const value = argument.trim();
    const nestedExpression = parseProjectionExpression(value, entity);
    if (nestedExpression) return nestedExpression;

    const quote = value[0];
    if ((quote === "'" || quote === "\"") && value.endsWith(quote)) {
      return { kind: "value", value: value.slice(1, -1).split(quote + quote).join(quote) };
    }
    if (entity.fields.some((field) => field.name === value)
      || /^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+$/.test(value)) {
      return { kind: "field", field: value };
    }
    if (/^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$/.test(value)) {
      return { kind: "value", value: Number(value) };
    }
    return { kind: "value", value };
  });

  return { kind: "function", function: functionMatch[1], arguments: arguments_ };
}

function parseProjectionColumn(text: string, entity: MetadataEntity): QueryColumn {
  const aliasMatch = text.match(/^(.+?)\s+AS\s+([A-Za-z_][A-Za-z0-9_]*)$/i)
    ?? text.match(/^(.+?)\s*\|\s*([A-Za-z_][A-Za-z0-9_]*)$/);
  const field = (aliasMatch?.[1] ?? text).trim();
  const expression = parseProjectionExpression(field, entity);
  return {
    field,
    alias: aliasMatch?.[2] ?? null,
    ...(expression ? { expression } : {}),
  };
}

function getProjectionRows(definition: QueryDefinition): string[][] {
  const rows = definition.projectionRows
    ? definition.projectionRows.map((row) => [...row])
    : definition.columns.length ? [definition.columns.map((column) => column.field)] : [];
  const availableFields = new Set(definition.columns.map((column) => column.field));
  const assignedFields = new Set<string>();

  for (let rowIndex = rows.length - 1; rowIndex >= 0; rowIndex -= 1) {
    const row = rows[rowIndex];
    for (let index = row.length - 1; index >= 0; index -= 1) {
      const field = row[index];
      if (!availableFields.has(field) || assignedFields.has(field)) row.splice(index, 1);
      else assignedFields.add(field);
    }
    if (!row.length) rows.splice(rowIndex, 1);
  }

  const missingFields = definition.columns
    .map((column) => column.field)
    .filter((field) => !assignedFields.has(field));
  if (missingFields.length) {
    if (rows.length) rows[rows.length - 1].push(...missingFields);
    else rows.push(missingFields);
  }
  return rows;
}

type ProjectionDropPosition = "left" | "right" | "above" | "below";

function getProjectionDropPosition(element: HTMLElement, clientX: number, clientY: number): ProjectionDropPosition {
  const bounds = element.getBoundingClientRect();
  const horizontalOffset = (clientX - bounds.left) / bounds.width;
  const verticalOffset = (clientY - bounds.top) / bounds.height;
  if (Math.abs(horizontalOffset - 0.5) >= Math.abs(verticalOffset - 0.5)) {
    return horizontalOffset < 0.5 ? "left" : "right";
  }
  return verticalOffset < 0.5 ? "above" : "below";
}

function getProjectionVisualRowFields(container: HTMLElement, targetField: string): string[] {
  const cards = Array.from(container.querySelectorAll<HTMLElement>(".field-card[data-projection-field]"));
  const target = cards.find((card) => card.dataset.projectionField === targetField);
  if (!target) return [targetField];

  const targetBounds = target.getBoundingClientRect();
  const targetCenter = targetBounds.top + targetBounds.height / 2;
  return cards
    .filter((card) => {
      const bounds = card.getBoundingClientRect();
      return Math.abs(bounds.top + bounds.height / 2 - targetCenter) < 2;
    })
    .map((card) => card.dataset.projectionField)
    .filter((field): field is string => field !== undefined);
}

function FieldCards({
  fields,
  rows,
  entity,
  metadata,
  functionDefinitions,
  functionDefinitionsStatus,
  functionDefinitionsError,
  definition,
  editingAliasField,
  setEditingAliasField,
  removeColumn,
  updateColumnAlias,
  commitColumnAlias,
  restoreColumnAlias,
  editingField,
  setEditingField,
  replaceColumn,
  restoreColumn,
  moveField,
  onFieldDragStart,
  editValue,
  applyExpression,
  addField,
  addValue,
}: {
  fields: { name: string }[];
  rows: string[][];
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  functionDefinitions: FieldBuilderExpressionOptions["functionDefinitions"];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  definition: QueryDefinition;
  editingAliasField: string | null;
  setEditingAliasField: (field: string | null) => void;
  removeColumn: (field: string) => void;
  updateColumnAlias: (field: string, alias: string) => void;
  commitColumnAlias: (field: string) => void;
  restoreColumnAlias: (field: string, alias: string | null) => void;
  editingField: string | null;
  setEditingField: (field: string | null) => void;
  replaceColumn: (field: string, replacement: string) => void;
  restoreColumn: (field: string, original: { field: string; alias: string | null }) => void;
  moveField: (field: string, targetField: string, position: ProjectionDropPosition, targetRowFields: string[]) => void;
  onFieldDragStart: (field: string | null) => void;
  editValue: (field: string, value: string | number) => void;
  applyExpression: (field: string | undefined, expression: QueryExpression, alias: string) => void;
  addField: (field: string) => void;
  addValue: (value: string | number) => void;
}) {
  const [draggedField, setDraggedField] = useState<string | null>(null);
  const [dropTarget, setDropTarget] = useState<{ field: string; position: ProjectionDropPosition } | null>(null);
  const [aliasBeforeEdit, setAliasBeforeEdit] = useState<string | null>(null);
  const [columnBeforeEdit, setColumnBeforeEdit] = useState<QueryColumn | null>(null);
  const columnBeforeEditRef = useRef<QueryColumn | null>(null);
  const startFieldEdit = (column: QueryColumn | undefined) => {
    const snapshot = column ?? null;
    columnBeforeEditRef.current = snapshot;
    setColumnBeforeEdit(snapshot);
    setEditingField(snapshot?.field ?? null);
  };

  const resetDrag = () => {
    setDraggedField(null);
    setDropTarget(null);
    onFieldDragStart(null);
  };
  const handleFieldDrop = (event: React.DragEvent, targetField: string) => {
    const field = event.dataTransfer.getData("application/x-query-field");
    if (field) {
      event.preventDefault();
      event.stopPropagation();
      if (field !== targetField) {
        const position = dropTarget?.field === targetField
          ? dropTarget.position
          : getProjectionDropPosition(event.currentTarget, event.clientX, event.clientY);
        const container = event.currentTarget.closest(".projection-rows");
        const targetRowFields = container
          ? getProjectionVisualRowFields(container, targetField)
          : [targetField];
        moveField(field, targetField, position, targetRowFields);
      }
      resetDrag();
    }
  };
  const handleProjectionDragOver = (event: React.DragEvent<HTMLDivElement>) => {
    if (!draggedField || (event.target instanceof Element && event.target.closest(".field-card"))) return;
    const cards = Array.from(event.currentTarget.querySelectorAll<HTMLElement>(".field-card"))
      .map((card) => ({
        element: card,
        field: card.dataset.projectionField,
        bounds: card.getBoundingClientRect(),
      }));
    if (!cards.length) return;

    const nearestCard = cards
      .map((card) => {
        const horizontalDistance = event.clientX < card.bounds.left
          ? card.bounds.left - event.clientX
          : event.clientX > card.bounds.right ? event.clientX - card.bounds.right : 0;
        const verticalDistance = event.clientY < card.bounds.top
          ? card.bounds.top - event.clientY
          : event.clientY > card.bounds.bottom ? event.clientY - card.bounds.bottom : 0;
        return {
          ...card,
          distance: Math.hypot(horizontalDistance / card.bounds.width, verticalDistance / card.bounds.height),
        };
      })
      .sort((left, right) => left.distance - right.distance)[0];

    if (nearestCard.field) {
      event.preventDefault();
      event.dataTransfer.dropEffect = "move";
      setDropTarget({
        field: nearestCard.field,
        position: getProjectionDropPosition(nearestCard.element, event.clientX, event.clientY),
      });
    }
  };

  return <div
    className="projection-rows"
    onDragEnd={resetDrag}
    onDragOver={handleProjectionDragOver}
    onDrop={(event) => {
      const field = event.dataTransfer.getData("application/x-query-field");
      if (field && dropTarget) {
        event.preventDefault();
        if (field !== dropTarget.field) {
          const targetRowFields = getProjectionVisualRowFields(event.currentTarget, dropTarget.field);
          moveField(field, dropTarget.field, dropTarget.position, targetRowFields);
        }
        resetDrag();
      }
    }}
  >{rows.map((row) => (
    <div className="projection-row" key={row[0]}>
      <div className="field-cards projection-row-fields">
        {row.map((fieldName) => {
    const field = fields.find((item) => item.name === fieldName);
    if (!field) return null;
    const column = definition.columns.find((item) => item.field === field.name);
    const columnExpression = column
      ? column.expression ?? parseProjectionExpression(column.field, entity)
      : undefined;
    const columnAlias = column ? getProjectionAlias(column) : null;
    const fieldLabel = columnExpression ? columnAlias || "calculated field" : field.name;
    const openColumnEditor = () => {
      if (!column) return;
      startFieldEdit(column);
    };
    const startAliasEdit = () => {
      setAliasBeforeEdit(columnAlias);
      setEditingAliasField(field.name);
    };
    return (
      <div
        className={`field-card${column ? " selected" : ""}${draggedField === field.name ? " dragging" : ""}${dropTarget?.field === field.name ? ` drop-target drop-target-${dropTarget.position}` : ""}`}
        key={field.name}
        data-projection-field={field.name}
        onDragOver={(event) => {
          if (draggedField && draggedField !== field.name) {
            event.preventDefault();
            const position = getProjectionDropPosition(event.currentTarget, event.clientX, event.clientY);
            event.dataTransfer.dropEffect = "move";
            setDropTarget({ field: field.name, position });
          }
        }}
        onDrop={(event) => {
          handleFieldDrop(event, field.name);
        }}
      >
        {column && (
          <span
            className="filter-field-drag-handle field-card-drag-handle"
            draggable
            aria-hidden="true"
            onDragStart={(event) => {
              setDraggedField(field.name);
              onFieldDragStart(field.name);
              event.dataTransfer.effectAllowed = "move";
              event.dataTransfer.setData("text/plain", field.name);
              event.dataTransfer.setData("application/x-query-field", field.name);
            }}
          >
          </span>
        )}
        <button type="button" className="field-card-remove" onClick={() => removeColumn(field.name)} aria-label={`Remove ${fieldLabel}`} title={`Remove ${fieldLabel}`}>
          <Icon name="close" size={12} />
        </button>
        <div className="field-card-details">
          <div
            className="field-card-primary"
            onClick={(event) => {
              if (column && !columnAlias && editingAliasField !== field.name && editingField !== field.name
                && !(event.target instanceof Element && event.target.closest(".field-name"))) {
                startAliasEdit();
              }
            }}
          >
            {column && editingAliasField === field.name
              ? <>
                <span className="field-name">{columnExpression ? formatExpression(columnExpression) : field.name}</span>
                {columnAlias && <span className="field-alias-separator" aria-hidden="true">|</span>}
                <input
                  autoFocus
                  className="field-alias-input"
                  aria-label={`Alias for ${fieldLabel}`}
                  placeholder="Alias"
                  value={columnAlias ?? ""}
                  onFocus={(event) => event.currentTarget.select()}
                  onChange={(event) => updateColumnAlias(field.name, event.target.value)}
                  onBlur={() => {
                    commitColumnAlias(field.name);
                    setEditingAliasField(null);
                  }}
                  onKeyDown={(event) => {
                    if (event.key === "Enter") event.currentTarget.blur();
                    if (event.key === "Escape") {
                      event.preventDefault();
                      restoreColumnAlias(field.name, aliasBeforeEdit);
                      setEditingAliasField(null);
                      setAliasBeforeEdit(null);
                    }
                  }}
                />
              </>
              : editingField === field.name
                ? <>
                  <FieldBuilder
                    entity={entity}
                    metadata={metadata}
                    value={columnExpression?.kind === "value" ? "" : field.name}
                    editMode
                    excludedPaths={definition.columns.filter((item) => item.field !== field.name).map((item) => item.field)}
                    allowValueSelection
                    allowCalculationSelection
                    initialSection={columnExpression && columnExpression.kind !== "value" ? "calculation" : undefined}
                    expressionBuilder={{
                      functionDefinitions,
                      functionDefinitionsStatus,
                      functionDefinitionsError,
                      initialExpression: columnExpression,
                      initialAlias: column
                        ? columnAlias ?? (columnExpression?.kind === "value" ? "CalculatedValue" : field.name)
                        : "CalculatedValue",
                      existingAliases: definition.columns
                        .filter((item) => item.field !== column?.field)
                        .map((item) => getProjectionAlias(item) ?? item.field),
                    }}
                    onExpressionApply={(expression, alias) => {
                      applyExpression(column?.field, expression, alias);
                      setEditingField(null);
                    }}
                    initialConstantValue={columnExpression?.kind === "value" ? columnExpression.value : undefined}
                    onValueSelect={(value) => {
                      editValue(field.name, value);
                      setEditingField(null);
                    }}
                    onChange={(replacement) => {
                      replaceColumn(field.name, replacement);
                      setEditingField(null);
                    }}
                    onCancel={() => {
                      const snapshot = columnBeforeEditRef.current ?? columnBeforeEdit;
                      if (snapshot) restoreColumn(field.name, snapshot);
                      setEditingField(null);
                      setColumnBeforeEdit(null);
                      columnBeforeEditRef.current = null;
                    }}
                  />
                  {columnAlias && <><span className="field-alias-separator" aria-hidden="true">|</span><span className="field-alias-value">{columnAlias}</span></>}
                </>
                : columnAlias
                  ? <><span
                    className="field-name editable"
                    onClick={openColumnEditor}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ")
                        openColumnEditor();
                    }}
                  >{columnExpression ? formatExpression(columnExpression) : field.name}</span><span className="field-alias-separator" aria-hidden="true">|</span><span
                    className="field-alias-value editable field-alias-placeholder"
                    onClick={startAliasEdit}
                    role="button"
                    tabIndex={0}
                    aria-label={`Edit alias for ${fieldLabel}`}
                    title={`Edit alias for ${fieldLabel}`}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        startAliasEdit();
                      }
                    }}
                  >{columnAlias}</span></>
                  : <span
                    className="field-name editable"
                    onClick={openColumnEditor}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ")
                        openColumnEditor();
                    }}
                  >{columnExpression ? formatExpression(columnExpression) : field.name}</span>}
            {column && !columnAlias && editingAliasField !== field.name && <>
              <button
                type="button"
                className="field-alias-value editable field-alias-placeholder"
                aria-label={`Add alias for ${fieldLabel}`}
                title={`Add alias for ${fieldLabel}`}
              />
            </>}
          </div>
        </div>
      </div>
    );
        })}
      </div>
    </div>
  ))}
    <div className="projection-row projection-row-builder">
      <div className="field-cards projection-row-fields">
        <FieldBuilder
          key={`${entity.name}-${definition.columns.length}`}
          entity={entity}
          metadata={metadata}
          value=""
          excludedPaths={definition.columns.map((column) => column.field)}
          placeholder="Field"
          placeholderIcon
          ariaLabel="Field"
          allowValueSelection
          allowCalculationSelection
          expressionBuilder={{
            functionDefinitions,
            functionDefinitionsStatus,
            functionDefinitionsError,
            initialAlias: "CalculatedValue",
            existingAliases: definition.columns.map((column) => getProjectionAlias(column) ?? column.field),
          }}
          onValueSelect={addValue}
          onExpressionApply={(expression, alias) => applyExpression(undefined, expression, alias)}
          onChange={addField}
        />
      </div>
    </div>
  </div>;
}

export default function ProjectionBuilder({ metadata, functionDefinitions, functionDefinitionsStatus, functionDefinitionsError, definition, entity, isReadOnly, onDefinitionChange, onFieldDragStart }: ProjectionBuilderProps) {
  const [editingAliasField, setEditingAliasField] = useState<string | null>(null);
  const [editingField, setEditingField] = useState<string | null>(null);
  const [isEditingProjectionText, setIsEditingProjectionText] = useState(false);
  const [projectionText, setProjectionText] = useState(() =>
    formatProjectionText(definition.columns, getProjectionRows(definition)));
  const selectedFields = definition.columns.map((column) => ({ name: column.field }));
  const projectionRows = getProjectionRows(definition);

  useEffect(() => {
    if (!isEditingProjectionText) {
      setProjectionText(formatProjectionText(definition.columns, projectionRows));
    }
  }, [definition.columns, definition.projectionRows, isEditingProjectionText]);

  const changeProjection = (nextDefinition: QueryDefinition, rows = projectionRows) => {
    const fields = new Set(nextDefinition.columns.map((column) => column.field));
    const nextRows = rows
      .map((row) => row.filter((field) => fields.has(field)))
      .filter((row) => row.length > 0);
    const assignedFields = new Set(nextRows.flat());
    const missingFields = nextDefinition.columns
      .map((column) => column.field)
      .filter((field) => !assignedFields.has(field));
    if (missingFields.length) {
      if (nextRows.length) nextRows[nextRows.length - 1].push(...missingFields);
      else nextRows.push(missingFields);
    }
    onDefinitionChange({ ...nextDefinition, projectionRows: nextRows });
  };

  const applyProjectionText = () => {
    const existingColumns = new Map(definition.columns.map((column) => [formatProjectionColumn(column), column]));
    const projectionRows = splitProjectionText(projectionText);
    const columnsByRow = projectionRows.map((row) => row.map((item) =>
      existingColumns.get(item) ?? parseProjectionColumn(item, entity)));
    const columns = columnsByRow.flat();
    changeProjection({ ...definition, columns }, columnsByRow.map((row) =>
      row.map((column) => column.field)));
    setIsEditingProjectionText(false);
  };

  const addColumn = (field: string) => {
    if (definition.columns.some((column) => column.field === field)) return;
    changeProjection(
      { ...definition, columns: [...definition.columns, { field, alias: null }] },
      [...getProjectionRows(definition), [field]],
    );
  };

  const addValue = (value: string | number) => {
    const field = `__value__${crypto.randomUUID()}`;
    const expression: QueryExpression = { kind: "value", value };
    changeProjection({
      ...definition,
      columns: [...definition.columns, { field, alias: null, expression }],
    });
  };

  const editValue = (field: string, value: string | number) => {
    const columns = definition.columns.map((column) => column.field === field
      ? { ...column, expression: { kind: "value" as const, value } }
      : column);
    changeProjection({ ...definition, columns });
  };

  const applyExpression = (field: string | undefined, expression: QueryExpression, alias: string) => {
    const outputField = field ?? `__calculated__${crypto.randomUUID()}`;
    const columns = field
      ? definition.columns.map((column) => column.field === outputField ? { ...column, alias, expression } : column)
      : [...definition.columns, { field: outputField, alias, expression }];
    changeProjection({ ...definition, columns });
  };

  const selectAllFields = () => {
    const selectedColumns = new Set(definition.columns.map((column) => column.field));
    const columns = [
      ...definition.columns,
      ...entity.fields
        .filter((field) => !selectedColumns.has(field.name))
        .map((field) => ({ field: field.name, alias: null })),
    ];
    if (columns.length !== definition.columns.length) changeProjection({ ...definition, columns });
  };

  const clearProjection = () => {
    if (definition.columns.length) changeProjection({ ...definition, columns: [] });
  };

  const removeColumn = (field: string) => {
    const columns = definition.columns.filter((column) => column.field !== field);
    changeProjection({ ...definition, columns });
  };

  const updateColumnAlias = (field: string, alias: string) => {
    const columns = definition.columns.map((column) => column.field === field
      ? { ...column, alias }
      : column);
    changeProjection({ ...definition, columns });
  };

  const commitColumnAlias = (field: string) => {
    const columns = definition.columns.map((column) => column.field === field
      ? { ...column, alias: column.alias?.trim() || null }
      : column);
    changeProjection({ ...definition, columns });
  };

  const restoreColumnAlias = (field: string, alias: string | null) => {
    const columns = definition.columns.map((column) => column.field === field
      ? { ...column, alias }
      : column);
    changeProjection({ ...definition, columns });
  };

  const replaceColumn = (field: string, replacement: string) => {
    if (definition.columns.some((column) => column.field === replacement)) return;
    const columns = definition.columns.map((column) => column.field === field
      ? { field: replacement, alias: null }
      : column);
    const rows = getProjectionRows(definition).map((row) =>
      row.map((item) => item === field ? replacement : item));
    changeProjection({ ...definition, columns }, rows);
  };

  const restoreColumn = (field: string, original: QueryColumn) => {
    const columns = definition.columns.map((column) => column.field === field
      ? original
      : column);
    onDefinitionChange({ ...definition, columns });
  };

  const moveField = (field: string, targetField: string, position: ProjectionDropPosition, targetRowFields: string[]) => {
    if (field === targetField) return;
    const rows = getProjectionRows(definition).map((row) => [...row]);
    const sourceRow = rows.findIndex((row) => row.includes(field));
    if (sourceRow < 0 || !rows.some((row) => row.includes(targetField))) return;
    const sourceIndex = rows[sourceRow].indexOf(field);
    rows[sourceRow].splice(sourceIndex, 1);
    if (!rows[sourceRow].length) rows.splice(sourceRow, 1);

    const targetRow = rows.findIndex((row) => row.includes(targetField));
    if (targetRow < 0) return;
    if (position === "above" || position === "below") {
      const row = rows[targetRow];
      const targetRowIndexes = row
        .map((item, index) => targetRowFields.includes(item) ? index : -1)
        .filter((index) => index >= 0);
      const targetIndex = row.indexOf(targetField);
      const boundary = position === "above"
        ? Math.min(...targetRowIndexes, targetIndex)
        : Math.max(...targetRowIndexes, targetIndex) + 1;
      const before = row.slice(0, boundary);
      const after = row.slice(boundary);
      const replacement = [before, [field], after].filter((items) => items.length > 0);
      rows.splice(targetRow, 1, ...replacement);
    } else {
      const targetIndex = rows[targetRow].indexOf(targetField);
      rows[targetRow].splice(targetIndex + (position === "right" ? 1 : 0), 0, field);
    }

    const columnsByField = new Map(definition.columns.map((column) => [column.field, column]));
    const columns = rows.flat().map((item) => columnsByField.get(item)).filter((column): column is QueryColumn => column !== undefined);
    changeProjection({ ...definition, columns }, rows);
  };

  return (
    <div className="builder-section projection-section">
      <div className="section-header">
        <div className="section-heading">
          <div className="projection-title-row">
            <span className="section-kicker">PROJECTION</span>
          </div>
          <p className="filter-description">
            {isReadOnly
              ? "Built-in query projection is read-only."
              : isEditingProjectionText
              ? "Edit fields or expressions, then switch back to Design UI to apply changes."
              : "Select fields to include. Drag a field to another field's left or right edge to reorder, or top or bottom edge to create a row."}
          </p>
        </div>
        <div className="section-header-actions">
          <div className="field-action-buttons">
          {!isEditingProjectionText && <>
            <button
              type="button"
              className="field-action field-action-icon"
              aria-label="Select all fields"
              title="Select all fields"
              onClick={selectAllFields}
              disabled={entity.fields.every((field) => definition.columns.some((column) => column.field === field.name))}
            >
              <Icon name="check" size={14} />
            </button>
            <button
              type="button"
              className="field-action field-action-icon"
              aria-label="Clear projection"
              title="Clear projection"
              onClick={clearProjection}
              disabled={!definition.columns.length}
            >
              <Icon name="close" size={14} />
            </button>
          </>}
          </div>
          <div className={`projection-text-control plan-text-mode-control${isEditingProjectionText ? " is-editing" : ""}`}>
            <button
              type="button"
              className="projection-text-action"
              aria-label={isEditingProjectionText ? "Show projection design" : "Edit projection as plan text"}
              aria-expanded={isEditingProjectionText}
              title={isEditingProjectionText ? "Show projection design" : "Edit projection as plan text"}
              onClick={() => {
                if (isEditingProjectionText) {
                  if (!isReadOnly) applyProjectionText();
                  else setIsEditingProjectionText(false);
                } else {
                  setProjectionText(formatProjectionText(definition.columns, projectionRows));
                  setIsEditingProjectionText(true);
                }
              }}
            >
              <Icon name="file-text" size={14} />
            </button>
          </div>
        </div>
      </div>
      {isEditingProjectionText ? (
        <div className="projection-plan-text-editor">
          {functionDefinitionsStatus === "failed" && (
            <p className="query-message error" role="alert">Function highlighting unavailable: {functionDefinitionsError}</p>
          )}
          <HighlightedTextArea
            metadata={metadata}
            entityName={entity.name}
            functionNames={functionDefinitions.map((definition) => definition.name)}
            value={projectionText}
            ariaLabel="Projection fields"
            disabled={isReadOnly}
            placeholder="e.g. Customer.Name, Count(*)"
            onChange={setProjectionText}
          />
        </div>
      ) : <div className="select-fields-row" inert={isReadOnly}>
        <FieldCards
          fields={selectedFields}
          rows={projectionRows}
          entity={entity}
          metadata={metadata}
          functionDefinitions={functionDefinitions}
          functionDefinitionsStatus={functionDefinitionsStatus}
          functionDefinitionsError={functionDefinitionsError}
          definition={definition}
          editingAliasField={editingAliasField}
          setEditingAliasField={setEditingAliasField}
          removeColumn={removeColumn}
          updateColumnAlias={updateColumnAlias}
          commitColumnAlias={commitColumnAlias}
          restoreColumnAlias={restoreColumnAlias}
          editingField={editingField}
          setEditingField={setEditingField}
          replaceColumn={replaceColumn}
          restoreColumn={restoreColumn}
          moveField={moveField}
          onFieldDragStart={onFieldDragStart}
          addField={addColumn}
          addValue={addValue}
          applyExpression={applyExpression}
          editValue={editValue}
        />
      </div>}
    </div>
  );
}
