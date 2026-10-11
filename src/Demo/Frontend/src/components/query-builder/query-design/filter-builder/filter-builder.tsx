import Icon from "../../../icon/icon";
import { MetadataEntity, QueryDefinition, QueryFilterGroup, QueryFilterItem, QueryFilterNode } from "../query-builder-slice";
import FieldBuilder from "../field-builder/field-builder";

const operators = [
  [0, "="],
  [1, "!="],
  [2, "contains"],
  [3, ">"],
  [4, "<"],
] as const;

type FilterBuilderProps = {
  metadata: MetadataEntity[];
  definition: QueryDefinition;
  entity: MetadataEntity;
  onDefinitionChange: (definition: QueryDefinition) => void;
};

const isGroup = (filter: QueryFilterNode): filter is QueryFilterGroup => "filters" in filter;

function createFilter(logicalOperator: number): QueryFilterItem {
  return {
    field: "",
    operation: 0,
    value: "",
    parameterized: false,
    logicalOperator,
    enabled: true,
  };
}

function createGroup(logicalOperation: number): QueryFilterGroup {
  return { logicalOperation, filters: [] };
}

type FilterGroupEditorProps = {
  group: QueryFilterGroup;
  metadata: MetadataEntity[];
  entity: MetadataEntity;
  onChange: (group: QueryFilterGroup) => void;
  onRemove?: () => void;
  depth: number;
};

function FilterGroupEditor({ group, metadata, entity, onChange, onRemove, depth }: FilterGroupEditorProps) {
  const updateNode = (index: number, node: QueryFilterNode) => {
    const filters = group.filters.map((filter, filterIndex) => filterIndex === index ? node : filter);
    onChange({ ...group, filters });
  };

  const removeNode = (index: number) => {
    onChange({ ...group, filters: group.filters.filter((_, filterIndex) => filterIndex !== index) });
  };

  const addFilter = () => {
    onChange({ ...group, filters: [...group.filters, createFilter(group.filters.length ? group.logicalOperation : 0)] });
  };

  const addGroup = () => {
    onChange({ ...group, filters: [...group.filters, createGroup(group.logicalOperation)] });
  };

  return (
    <div className={`filter-group filter-group-depth-${Math.min(depth, 3)}`}>
      <div className="filter-group-header">
        <span className="filter-group-title">{depth === 0 ? "All filters" : "Filter group"}</span>
        <label className="filter-group-logic">
          <span>Match</span>
          <select
            aria-label={`${depth === 0 ? "Root" : "Nested"} group logical operator`}
            value={group.logicalOperation}
            onChange={(event) => onChange({ ...group, logicalOperation: Number(event.target.value) })}
          >
            <option value={0}>ALL (AND)</option>
            <option value={1}>ANY (OR)</option>
          </select>
        </label>
        <button className="text-button" type="button" onClick={addFilter}><Icon name="plus" size={12} /> Condition</button>
        <button className="text-button" type="button" onClick={addGroup}><Icon name="plus" size={12} /> Group</button>
        {onRemove && <button className="icon-button" type="button" aria-label="Remove filter group" onClick={onRemove}><Icon name="trash" size={13} /></button>}
      </div>

      {!group.filters.length && <p className="empty-builder">Add a condition or nested group.</p>}
      {group.filters.map((filter, index) => isGroup(filter) ? (
        <FilterGroupEditor
          key={`group-${index}`}
          group={filter}
          metadata={metadata}
          entity={entity}
          depth={depth + 1}
          onChange={(nested) => updateNode(index, nested)}
          onRemove={() => removeNode(index)}
        />
      ) : (
        <FilterRow
          key={`filter-${index}`}
          filter={filter}
          first={index === 0}
          metadata={metadata}
          entity={entity}
          onChange={(next) => updateNode(index, next)}
          onRemove={() => removeNode(index)}
        />
      ))}
    </div>
  );
}

type FilterRowProps = {
  filter: QueryFilterItem;
  first: boolean;
  metadata: MetadataEntity[];
  entity: MetadataEntity;
  onChange: (filter: QueryFilterItem) => void;
  onRemove: () => void;
};

function FilterRow({ filter, first, metadata, entity, onChange, onRemove }: FilterRowProps) {
  const update = (changes: Partial<QueryFilterItem>) => onChange({ ...filter, ...changes });

  return (
    <div className={`filter-row${filter.enabled ? "" : " filter-row-disabled"}`}>
      {first ? (
        <span className="logical-operator-spacer" aria-hidden="true" />
      ) : (
        <select
          className="logical-operator"
          aria-label="Logical operator before condition"
          value={filter.logicalOperator}
          onChange={(event) => update({ logicalOperator: Number(event.target.value) })}
        >
          <option value={0}>AND</option>
          <option value={1}>OR</option>
        </select>
      )}
      <FieldBuilder
        entity={entity}
        metadata={metadata}
        value={filter.field}
        showPath
        showDataType
        ariaLabel="Filter field"
        onChange={(field) => update({ field })}
        onRemove={() => update({ field: "" })}
      />
      <select value={filter.operation} aria-label="Filter operation" onChange={(event) => update({ operation: Number(event.target.value) })}>
        {operators.map(([value, label]) => <option value={value} key={value}>{label}</option>)}
      </select>
      <input value={filter.value} placeholder="Value" aria-label="Filter value" onChange={(event) => update({ value: event.target.value })} />
      <button
        className="filter-toggle"
        type="button"
        aria-label={filter.enabled ? "Disable filter" : "Enable filter"}
        aria-pressed={filter.enabled}
        title={filter.enabled ? "Disable filter" : "Enable filter"}
        onClick={() => update({ enabled: !filter.enabled })}
      >
        {filter.enabled ? "ON" : "OFF"}
      </button>
      <button className="icon-button" type="button" aria-label="Remove filter" onClick={onRemove}><Icon name="trash" size={13} /></button>
    </div>
  );
}

export default function FilterBuilder({ metadata, definition, entity, onDefinitionChange }: FilterBuilderProps) {
  return (
    <div className="builder-section filters-section">
      <div className="section-header">
        <span className="section-kicker">FILTERS</span>
      </div>
      <FilterGroupEditor
        group={definition.filters}
        metadata={metadata}
        entity={entity}
        depth={0}
        onChange={(filters) => onDefinitionChange({ ...definition, filters })}
      />
    </div>
  );
}
