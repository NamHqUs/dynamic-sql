import { useState } from "react";
import Icon from "../../../icon/icon";
import { MetadataEntity, MetadataField, QueryDefinition } from "../query-builder-slice";
import FieldBox, { resolveEntity } from "../field-box/field-box";

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
  toggleColumn,
  updateColumnAlias,
  commitColumnAlias,
}: {
  fields: MetadataField[];
  definition: QueryDefinition;
  editingAliasField: string | null;
  setEditingAliasField: (field: string | null) => void;
  toggleColumn: (field: string) => void;
  updateColumnAlias: (field: string, alias: string) => void;
  commitColumnAlias: (field: string) => void;
}) {
  return <div className="field-cards">{fields.map((field) => {
    const column = definition.columns.find((item) => item.field === field.name);
    return (
      <div className={`field-card${column ? " selected" : ""}`} key={field.name}>
        <button type="button" className="field-card-toggle" onClick={() => toggleColumn(field.name)} aria-pressed={!!column}>
          <Icon name={column ? "check" : "plus"} size={12} />
        </button>
        <div className="field-card-details">
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
          <span className="field-card-type">{field.dataType}</span>
        </div>
      </div>
    );
  })}</div>;
}

export default function SelectFieldBuilder({ metadata, definition, entity, onDefinitionChange }: SelectFieldBuilderProps) {
  const [editingAliasField, setEditingAliasField] = useState<string | null>(null);
  const selectedFields = definition.columns.map((column) => {
    const segments = column.field.split(".");
    const owner = resolveEntity(entity, metadata, segments.slice(0, -1));
    const field = owner?.fields.find((item) => item.name === segments[segments.length - 1]);
    return { name: column.field, dataType: field?.dataType ?? "" };
  });

  const addColumn = (field: string) => {
    if (definition.columns.some((column) => column.field === field)) return;
    onDefinitionChange({ ...definition, columns: [...definition.columns, { field, alias: null }] });
  };

  const toggleColumn = (field: string) => {
    const columns = definition.columns.some((column) => column.field === field)
      ? definition.columns.filter((column) => column.field !== field)
      : [...definition.columns, { field, alias: null }];
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
          toggleColumn={toggleColumn}
          updateColumnAlias={updateColumnAlias}
          commitColumnAlias={commitColumnAlias}
        />
        <FieldBox key={`${entity.name}-${definition.columns.length}`} entity={entity} metadata={metadata} value="" clearOnSelect excludedPaths={definition.columns.map((column) => column.field)} placeholder="Add Field" ariaLabel="Add column" onChange={addColumn} />
      </div>
    </div>
  );
}
