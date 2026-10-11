import { useRef, useState } from "react";
import Icon from "../../../icon/icon";
import { MetadataEntity, QueryDefinition } from "../query-builder-slice";
import FieldBuilder from "../field-builder/field-builder";

type ProjectionBuilderProps = {
  metadata: MetadataEntity[];
  definition: QueryDefinition;
  entity: MetadataEntity;
  onDefinitionChange: (definition: QueryDefinition) => void;
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
}) {
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
  const [aliasBeforeEdit, setAliasBeforeEdit] = useState<string | null>(null);
  const [columnBeforeEdit, setColumnBeforeEdit] = useState<{ field: string; alias: string | null } | null>(null);
  const columnBeforeEditRef = useRef<{ field: string; alias: string | null } | null>(null);
  const startFieldEdit = (column: { field: string; alias: string | null } | undefined) => {
    const snapshot = column ?? null;
    columnBeforeEditRef.current = snapshot;
    setColumnBeforeEdit(snapshot);
    setEditingField(snapshot?.field ?? null);
  };

  return <div className="field-cards" onDragEnd={() => setDraggedIndex(null)}>{fields.map((field, index) => {
    const column = definition.columns.find((item) => item.field === field.name);
    return (
      <div
        className={`field-card${column ? " selected" : ""}${draggedIndex === index ? " dragging" : ""}`}
        key={field.name}
        draggable={Boolean(column)}
        onDragStart={(event) => {
          setDraggedIndex(index);
          event.dataTransfer.effectAllowed = "move";
          event.dataTransfer.setData("text/plain", field.name);
        }}
        onDragOver={(event) => {
          if (draggedIndex !== null && draggedIndex !== index) event.preventDefault();
        }}
        onDrop={(event) => {
          event.preventDefault();
          if (draggedIndex !== null && draggedIndex !== index) moveColumn(draggedIndex, index);
          setDraggedIndex(null);
        }}
      >
        <button type="button" className="field-card-remove" onClick={() => removeColumn(field.name)} aria-label={`Remove ${field.name}`} title={`Remove ${field.name}`}>
          <Icon name="close" size={12} />
        </button>
        <button
          type="button"
          className="field-card-edit"
          onClick={() => {
            setAliasBeforeEdit(column?.alias ?? null);
            setEditingAliasField(field.name);
          }}
          aria-label={`Edit alias for ${field.name}`}
          title={`Edit alias for ${field.name}`}
        >
          <Icon name="edit" size={12} />
        </button>
        <div className="field-card-details">
          <div className="field-card-primary">
            {column && editingAliasField === field.name
              ? <>
                <span className="field-name">{field.name}</span>
                <span className="field-alias-separator" aria-hidden="true">|</span>
                <input
                  autoFocus
                  className="field-alias-input"
                  aria-label={`Alias for ${field.name}`}
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
                ? <FieldBuilder
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
                : column?.alias
                  ? <><span
                    className="field-name editable"
                    onClick={() => startFieldEdit(column)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ") startFieldEdit(column);
                    }}
                  >{field.name}</span><span className="field-alias-separator" aria-hidden="true">|</span><span className="field-alias-value">{column.alias}</span></>
                  : <span
                    className="field-name editable"
                    onClick={() => startFieldEdit(column)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ") startFieldEdit(column);
                    }}
                  >{field.name}</span>}
          </div>
        </div>
      </div>
    );
  })}</div>;
}

export default function ProjectionBuilder({ metadata, definition, entity, onDefinitionChange }: ProjectionBuilderProps) {
  const [editingAliasField, setEditingAliasField] = useState<string | null>(null);
  const [editingField, setEditingField] = useState<string | null>(null);
  const selectedFields = definition.columns.map((column) => ({ name: column.field }));

  const addColumn = (field: string) => {
    if (definition.columns.some((column) => column.field === field)) return;
    onDefinitionChange({ ...definition, columns: [...definition.columns, { field, alias: null }] });
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
        <button
          type="button"
          className="button primary"
          onClick={selectAllFields}
          disabled={entity.fields.every((field) => definition.columns.some((column) => column.field === field.name))}
        >
          <Icon name="check" size={12} /> Select all fields
        </button>
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
        />
        <FieldBuilder
          key={`${entity.name}-${definition.columns.length}`}
          entity={entity}
          metadata={metadata}
          value=""
          excludedPaths={definition.columns.map((column) => column.field)}
          placeholder="Add Field"
          ariaLabel="Add column"
          onChange={addColumn}
        />
      </div>
    </div>
  );
}
