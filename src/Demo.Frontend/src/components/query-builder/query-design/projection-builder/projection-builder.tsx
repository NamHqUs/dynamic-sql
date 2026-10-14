import { useRef, useState } from "react";
import Icon from "../../../icon/icon";
import { MetadataEntity, QueryDefinition } from "../query-builder-slice";
import FieldBuilder from "../field-builder/field-builder";
import ExpressionBuilder, { formatExpression } from "../field-builder/expression-builder";
import { QueryExpression } from "../query-builder-slice";

type ProjectionBuilderProps = {
  metadata: MetadataEntity[];
  definition: QueryDefinition;
  entity: MetadataEntity;
  onDefinitionChange: (definition: QueryDefinition) => void;
  onFieldDragStart: (field: string | null) => void;
};

function FieldCards({
  fields,
  entity,
  metadata,
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
  moveColumn,
  onFieldDragStart,
  editExpression,
}: {
  fields: { name: string }[];
  entity: MetadataEntity;
  metadata: MetadataEntity[];
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
  moveColumn: (fromIndex: number, toIndex: number) => void;
  onFieldDragStart: (field: string | null) => void;
  editExpression: (column: { field: string; alias: string | null; expression?: QueryExpression }) => void;
}) {
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
  const [dropTargetIndex, setDropTargetIndex] = useState<number | null>(null);
  const [aliasBeforeEdit, setAliasBeforeEdit] = useState<string | null>(null);
  const [columnBeforeEdit, setColumnBeforeEdit] = useState<{ field: string; alias: string | null } | null>(null);
  const columnBeforeEditRef = useRef<{ field: string; alias: string | null } | null>(null);
  const startFieldEdit = (column: { field: string; alias: string | null } | undefined) => {
    const snapshot = column ?? null;
    columnBeforeEditRef.current = snapshot;
    setColumnBeforeEdit(snapshot);
    setEditingField(snapshot?.field ?? null);
  };

  return <div className="field-cards" onDragEnd={() => {
    setDraggedIndex(null);
    setDropTargetIndex(null);
    onFieldDragStart(null);
  }}>{fields.map((field, index) => {
    const column = definition.columns.find((item) => item.field === field.name);
    const fieldLabel = column?.expression ? column.alias || "calculated field" : field.name;
    return (
      <div
        className={`field-card${column ? " selected" : ""}${draggedIndex === index ? " dragging" : ""}${dropTargetIndex === index && draggedIndex !== index ? " drop-target" : ""}`}
        key={field.name}
        onDragOver={(event) => {
          if (draggedIndex !== null && draggedIndex !== index) {
            event.preventDefault();
            setDropTargetIndex(index);
          }
        }}
        onDrop={(event) => {
          event.preventDefault();
          const draggedField = event.dataTransfer.getData("application/x-query-field")
            || event.dataTransfer.getData("text/plain");
          const sourceIndex = fields.findIndex((item) => item.name === draggedField);
          if (sourceIndex >= 0 && sourceIndex !== index) moveColumn(sourceIndex, index);
          setDraggedIndex(null);
          setDropTargetIndex(null);
        }}
      >
        {column && (
          <span
            className="filter-field-drag-handle field-card-drag-handle"
            draggable
            aria-hidden="true"
            onDragStart={(event) => {
              setDraggedIndex(index);
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
        <button
          type="button"
          className="field-card-edit"
          onClick={() => {
            setAliasBeforeEdit(column?.alias ?? null);
            setEditingAliasField(field.name);
          }}
          aria-label={`Edit alias for ${fieldLabel}`}
          title={`Edit alias for ${fieldLabel}`}
        >
          <Icon name="edit" size={12} />
        </button>
        <div className="field-card-details">
          <div className="field-card-primary">
            {column && editingAliasField === field.name
              ? <>
                <span className="field-name">{column.expression ? formatExpression(column.expression) : field.name}</span>
                <span className="field-alias-separator" aria-hidden="true">|</span>
                <input
                  autoFocus
                  className="field-alias-input"
                  aria-label={`Alias for ${fieldLabel}`}
                  placeholder="Alias"
                  value={column.alias ?? ""}
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
                    value={field.name}
                    editMode
                    excludedPaths={definition.columns.filter((item) => item.field !== field.name).map((item) => item.field)}
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
                  {column?.alias && <><span className="field-alias-separator" aria-hidden="true">|</span><span className="field-alias-value">{column.alias}</span></>}
                </>
                : column?.alias
                  ? <><span
                    className="field-name editable"
                    onClick={() => column.expression ? editExpression(column) : startFieldEdit(column)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ")
                        column.expression ? editExpression(column) : startFieldEdit(column);
                    }}
                  >{column.expression ? formatExpression(column.expression) : field.name}</span><span className="field-alias-separator" aria-hidden="true">|</span><span className="field-alias-value">{column.alias}</span></>
                  : <span
                    className="field-name editable"
                    onClick={() => column?.expression ? editExpression(column) : startFieldEdit(column)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ")
                        column?.expression ? editExpression(column) : startFieldEdit(column);
                    }}
                  >{column?.expression ? formatExpression(column.expression) : field.name}</span>}
          </div>
        </div>
      </div>
    );
  })}</div>;
}

export default function ProjectionBuilder({ metadata, definition, entity, onDefinitionChange, onFieldDragStart }: ProjectionBuilderProps) {
  const [editingAliasField, setEditingAliasField] = useState<string | null>(null);
  const [editingField, setEditingField] = useState<string | null>(null);
  const [editingExpression, setEditingExpression] = useState<{ field: string | null; expression?: QueryExpression; alias?: string } | null>(null);
  const selectedFields = definition.columns.map((column) => ({ name: column.field }));

  const addColumn = (field: string) => {
    if (definition.columns.some((column) => column.field === field)) return;
    onDefinitionChange({ ...definition, columns: [...definition.columns, { field, alias: null }] });
  };

  const applyExpression = (expression: QueryExpression, alias: string) => {
    const editing = editingExpression;
    const field = editing?.field ?? `__calculated__${crypto.randomUUID()}`;
    const columns = editing?.field
      ? definition.columns.map((column) => column.field === field ? { ...column, alias, expression } : column)
      : [...definition.columns, { field, alias, expression }];
    onDefinitionChange({ ...definition, columns });
    setEditingExpression(null);
  };

  const selectAllFields = () => {
    const selectedColumns = new Set(definition.columns.map((column) => column.field));
    const columns = [
      ...definition.columns,
      ...entity.fields
        .filter((field) => !selectedColumns.has(field.name))
        .map((field) => ({ field: field.name, alias: null })),
    ];
    if (columns.length !== definition.columns.length) onDefinitionChange({ ...definition, columns });
  };

  const clearProjection = () => {
    if (definition.columns.length) onDefinitionChange({ ...definition, columns: [] });
  };

  const removeColumn = (field: string) => {
    const columns = definition.columns.filter((column) => column.field !== field);
    onDefinitionChange({ ...definition, columns });
  };

  const updateColumnAlias = (field: string, alias: string) => {
    const columns = definition.columns.map((column) => column.field === field
      ? { ...column, alias }
      : column);
    onDefinitionChange({ ...definition, columns });
  };

  const commitColumnAlias = (field: string) => {
    const columns = definition.columns.map((column) => column.field === field
      ? { ...column, alias: column.alias?.trim() || null }
      : column);
    onDefinitionChange({ ...definition, columns });
  };

  const restoreColumnAlias = (field: string, alias: string | null) => {
    const columns = definition.columns.map((column) => column.field === field
      ? { ...column, alias }
      : column);
    onDefinitionChange({ ...definition, columns });
  };

  const replaceColumn = (field: string, replacement: string) => {
    if (definition.columns.some((column) => column.field === replacement)) return;
    const columns = definition.columns.map((column) => column.field === field
      ? { field: replacement, alias: null }
      : column);
    onDefinitionChange({ ...definition, columns });
  };

  const restoreColumn = (field: string, original: { field: string; alias: string | null }) => {
    const columns = definition.columns.map((column) => column.field === field
      ? original
      : column);
    onDefinitionChange({ ...definition, columns });
  };

  const moveColumn = (fromIndex: number, toIndex: number) => {
    if (fromIndex < 0 || toIndex < 0 || fromIndex >= definition.columns.length || toIndex >= definition.columns.length) return;
    const columns = [...definition.columns];
    const [column] = columns.splice(fromIndex, 1);
    columns.splice(toIndex, 0, column);
    onDefinitionChange({ ...definition, columns });
  };

  return (
    <div className="builder-section">
      <div className="section-header">
        <div className="section-heading">
          <span className="section-kicker">SELECT FIELDS</span>
          <p className="filter-description">Select a field to add it to the query, or select a relation to browse and choose fields from the related entity.</p>
        </div>
        <div className="field-action-buttons">
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
        </div>
      </div>
      <div className="select-fields-row">
        <FieldCards
          fields={selectedFields}
          entity={entity}
          metadata={metadata}
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
          moveColumn={moveColumn}
          onFieldDragStart={onFieldDragStart}
          editExpression={(column) => setEditingExpression({
            field: column.field,
            expression: column.expression,
            alias: column.alias ?? "",
          })}
        />
        <FieldBuilder
          key={`${entity.name}-${definition.columns.length}`}
          entity={entity}
          metadata={metadata}
          value=""
          excludedPaths={definition.columns.map((column) => column.field)}
          placeholder="Display field"
          placeholderIcon
          ariaLabel="Display field"
          onChange={addColumn}
        />
        <button
          type="button"
          className="field-action expression-add-button"
          onClick={() => setEditingExpression({ field: null })}
        >
          <span aria-hidden="true">ƒx</span> Add calculation
        </button>
      </div>
      <div className="group-by-row">
        <div>
          <span className="section-kicker">GROUP BY</span>
          <p className="filter-description">Choose the fields that identify each aggregate row.</p>
        </div>
        <div className="group-by-fields">
          {definition.groupBy.map((field) => (
            <span className="group-by-chip" key={field}>
              {field}
              <button
                type="button"
                aria-label={`Remove ${field} from group by`}
                title={`Remove ${field} from group by`}
                onClick={() => onDefinitionChange({ ...definition, groupBy: definition.groupBy.filter((item) => item !== field) })}
              >
                <Icon name="close" size={11} />
              </button>
            </span>
          ))}
          <FieldBuilder
            key={`${entity.name}-group-${definition.groupBy.length}`}
            entity={entity}
            metadata={metadata}
            value=""
            ariaLabel="Group by field"
            placeholder="Add group field"
            allowAggregates={false}
            excludedPaths={definition.groupBy}
            onChange={(field) => {
              if (!definition.groupBy.includes(field))
                onDefinitionChange({ ...definition, groupBy: [...definition.groupBy, field] });
            }}
          />
        </div>
      </div>
      {editingExpression && (
        <ExpressionBuilder
          key={editingExpression.field ?? "new-expression"}
          entity={entity}
          metadata={metadata}
          initialExpression={editingExpression.expression}
          initialAlias={editingExpression.alias}
          existingAliases={definition.columns
            .filter((column) => column.field !== editingExpression.field)
            .map((column) => column.alias || column.field)}
          onApply={applyExpression}
          onCancel={() => setEditingExpression(null)}
        />
      )}
    </div>
  );
}
