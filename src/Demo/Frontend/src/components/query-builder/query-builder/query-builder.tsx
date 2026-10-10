import { useState } from "react";
import Icon from "../../icon/icon";
import { MetadataEntity, MetadataField, QueryDefinition } from "./query-builder-slice";
import "./query-builder.css";

const operators = [
  [0, "="],
  [1, "!="],
  [2, "contains"],
  [3, ">"],
  [4, "<"],
] as const;

type QueryBuilderFormProps = {
  metadata: MetadataEntity[];
  metadataStatus: string;
  metadataError: string;
  definition: QueryDefinition;
  entity?: MetadataEntity;
  onDefinitionChange: (definition: QueryDefinition) => void;
};

type FieldBrowserProps = {
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  path: string;
  expandedPaths: Set<string>;
  onToggleRelation: (path: string) => void;
  definition: QueryDefinition;
  editingAliasField: string | null;
  setEditingAliasField: (field: string | null) => void;
  toggleColumn: (field: string) => void;
  updateColumnAlias: (field: string, alias: string) => void;
  commitColumnAlias: (field: string) => void;
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

function FieldBrowser({
  entity,
  metadata,
  path,
  expandedPaths,
  onToggleRelation,
  ...fieldCardProps
}: FieldBrowserProps) {
  return <div className="field-browser">
    <FieldCards fields={entity.fields.map((field) => ({ ...field, name: path ? `${path}.${field.name}` : field.name }))} {...fieldCardProps} />
    {(entity.relations || []).map((relation) => {
      const relatedEntity = metadata.find((item) => item.name === relation.relatedEntity);
      if (!relatedEntity) return null;
      const relationPath = path ? `${path}.${relation.name}` : relation.name;
      const expanded = expandedPaths.has(relationPath);
      return (
        <div className="relation-browser" key={relationPath}>
          <button type="button" className="relation-toggle" onClick={() => onToggleRelation(relationPath)} aria-expanded={expanded}>
            <Icon name={expanded ? "chevron-down" : "chevron-right"} size={12} />
            <span>{relation.name}</span>
            <span className="relation-target">{relatedEntity.name}</span>
          </button>
          {expanded && <FieldBrowser entity={relatedEntity} metadata={metadata} path={relationPath} expandedPaths={expandedPaths} onToggleRelation={onToggleRelation} {...fieldCardProps} />}
        </div>
      );
    })}
  </div>;
}

export default function QueryBuilderForm({
  metadata,
  metadataStatus,
  metadataError,
  definition,
  entity,
  onDefinitionChange,
}: QueryBuilderFormProps) {
  const [editingAliasField, setEditingAliasField] = useState<string | null>(null);
  const [expandedRelationPaths, setExpandedRelationPaths] = useState<Set<string>>(new Set());

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

  const updateFilter = (index: number, key: string, value: string | number) => {
    const filters = definition.filters.map((filter, filterIndex) => filterIndex === index ? { ...filter, [key]: value } : filter);
    onDefinitionChange({ ...definition, filters });
  };

  return (
    <section className="query-builder-card">
      <div className="query-builder-toolbar">
        <label>Entity
          <select value={definition.entity} onChange={(event) => {
            const next = metadata.find((item) => item.name === event.target.value);
            onDefinitionChange({
              entity: event.target.value,
              columns: (next?.fields || []).slice(0, 5).map((field) => ({ field: field.name, alias: null })),
              filters: [],
              sorts: [],
              page: null,
              pageSize: 50,
              includeArchive: false,
            });
            setExpandedRelationPaths(new Set());
          }}>
            {!metadata.length && <option value="">Select an entity</option>}
            {metadata.map((item) => <option value={item.name} key={item.name}>{item.name}</option>)}
          </select>
        </label>
        <label className="checkbox-label"><input type="checkbox" checked={definition.includeArchive} onChange={(event) => onDefinitionChange({ ...definition, includeArchive: event.target.checked })} /> Include archived</label>
        <label>Page size <input className="small-input" type="number" min="1" max="1000" value={definition.pageSize || ""} onChange={(event) => onDefinitionChange({ ...definition, pageSize: event.target.value ? Number(event.target.value) : null })} /></label>
      </div>

      {metadataStatus === "loading" && <p className="query-message">Loading metadata…</p>}
      {metadataStatus === "failed" && <p className="query-message error" role="alert">{metadataError}</p>}
      {entity && <div className="query-builder-body">
        <div className="builder-section">
          <span className="section-kicker">SELECT FIELDS</span>
          <p className="helper">Select the columns to return. Expand a relation to browse its fields.</p>
          <FieldBrowser
            entity={entity}
            metadata={metadata}
            path=""
            expandedPaths={expandedRelationPaths}
            onToggleRelation={(path) => setExpandedRelationPaths((current) => {
              const next = new Set(current);
              if (next.has(path)) next.delete(path); else next.add(path);
              return next;
            })}
            definition={definition}
            editingAliasField={editingAliasField}
            setEditingAliasField={setEditingAliasField}
            toggleColumn={toggleColumn}
            updateColumnAlias={updateColumnAlias}
            commitColumnAlias={commitColumnAlias}
          />
        </div>
        <div className="builder-section filters-section">
          <div className="section-header"><span className="section-kicker">FILTERS</span><button className="text-button" onClick={() => onDefinitionChange({ ...definition, filters: [...definition.filters, { field: entity.fields[0]?.name || "", operator: 0, value: "", parameterized: false }] })}><Icon name="plus" size={12} /> Add filter</button></div>
          {definition.filters.map((filter, index) => (
            <div className="filter-row" key={index}>
              <select value={filter.field} onChange={(event) => updateFilter(index, "field", event.target.value)}>{entity.fields.map((field) => <option key={field.name}>{field.name}</option>)}</select>
              <select value={filter.operator} onChange={(event) => updateFilter(index, "operator", Number(event.target.value))}>{operators.map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select>
              <input value={filter.value} placeholder="Value" onChange={(event) => updateFilter(index, "value", event.target.value)} />
              <button className="icon-button" aria-label="Remove filter" onClick={() => onDefinitionChange({ ...definition, filters: definition.filters.filter((_, filterIndex) => filterIndex !== index) })}><Icon name="trash" size={13} /></button>
            </div>
          ))}
          {!definition.filters.length && <p className="empty-builder">No filters. All records will be considered.</p>}
        </div>
      </div>}
    </section>
  );
}
