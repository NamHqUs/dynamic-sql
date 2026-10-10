import { useState } from "react";
import Icon from "../../../icon/icon";
import { MetadataEntity, QueryDefinition } from "../query-builder-slice";
import FieldBuilder from "../field-builder/field-builder";

type SelectFieldBuilderProps = {
  metadata: MetadataEntity[];
  definition: QueryDefinition;
  entity: MetadataEntity;
  onDefinitionChange: (definition: QueryDefinition) => void;
};

function FieldCards({
  fields,
  definition,
  editingAliasField,
  setEditingAliasField,
  removeColumn,
  updateColumnAlias,
  commitColumnAlias,
  moveColumn,
}: {
  fields: { name: string }[];
  definition: QueryDefinition;
  editingAliasField: string | null;
  setEditingAliasField: (field: string | null) => void;
  removeColumn: (field: string) => void;
  updateColumnAlias: (field: string, alias: string) => void;
  commitColumnAlias: (field: string) => void;
  moveColumn: (fromIndex: number, toIndex: number) => void;
}) {
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

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
        <div className="field-card-details">
          <div className="field-card-primary">
            {column && editingAliasField === field.name
              ? <input
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
                }}
              />
              : column?.alias
                ? <><span className="field-name">{field.name}</span><span className="field-alias-separator" aria-hidden="true">|</span><span
                  className="field-alias-value editable"
                  onClick={() => setEditingAliasField(field.name)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(event) => {
                    if (event.key === "Enter" || event.key === " ") setEditingAliasField(field.name);
                  }}
                >{column.alias}</span></>
                : <span
                  className={`field-name${column ? " editable" : ""}`}
                  onClick={() => column && setEditingAliasField(field.name)}
                  role={column ? "button" : undefined}
                  tabIndex={column ? 0 : undefined}
                  onKeyDown={(event) => {
                    if (column && (event.key === "Enter" || event.key === " ")) setEditingAliasField(field.name);
                  }}
                >{field.name}</span>}
          </div>
        </div>
      </div>
    );
  })}</div>;
}

export default function SelectFieldBuilder({ metadata, definition, entity, onDefinitionChange }: SelectFieldBuilderProps) {
  const [editingAliasField, setEditingAliasField] = useState<string | null>(null);
  const selectedFields = definition.columns.map((column) => ({ name: column.field }));

  const addColumn = (field: string) => {
    if (definition.columns.some((column) => column.field === field)) return;
    onDefinitionChange({ ...definition, columns: [...definition.columns, { field, alias: null }] });
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

  const moveColumn = (fromIndex: number, toIndex: number) => {
    if (fromIndex < 0 || toIndex < 0 || fromIndex >= definition.columns.length || toIndex >= definition.columns.length) return;
    const columns = [...definition.columns];
    const [column] = columns.splice(fromIndex, 1);
    columns.splice(toIndex, 0, column);
    onDefinitionChange({ ...definition, columns });
  };

  return (
    <div className="builder-section">
      <span className="section-kicker">SELECT FIELDS</span>
      <p className="helper">Pick a field, or a relation to continue into its entity.</p>
      <div className="select-fields-row">
        <FieldCards
          fields={selectedFields}
          definition={definition}
          editingAliasField={editingAliasField}
          setEditingAliasField={setEditingAliasField}
          removeColumn={removeColumn}
          updateColumnAlias={updateColumnAlias}
          commitColumnAlias={commitColumnAlias}
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
