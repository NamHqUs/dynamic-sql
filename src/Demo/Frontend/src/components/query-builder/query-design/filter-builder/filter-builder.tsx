import Icon from "../../../icon/icon";
import { MetadataEntity, QueryDefinition } from "../query-builder-slice";
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

export default function FilterBuilder({ metadata, definition, entity, onDefinitionChange }: FilterBuilderProps) {
  const updateFilter = (index: number, key: string, value: string | number | boolean | null) => {
    const filters = definition.filters.map((filter, filterIndex) => filterIndex === index ? { ...filter, [key]: value } : filter);
    onDefinitionChange({ ...definition, filters });
  };

  return (
    <div className="builder-section filters-section">
      <div className="section-header"><span className="section-kicker">FILTERS</span><button className="text-button" onClick={() => onDefinitionChange({ ...definition, filters: [...definition.filters, { field: "", operator: 0, value: "", parameterized: false, logicalOperator: definition.filters.length ? 0 : null, enabled: true }] })}><Icon name="plus" size={12} /> Add filter</button></div>
      {definition.filters.map((filter, index) => (
        <div className={`filter-row${filter.enabled !== false ? "" : " filter-row-disabled"}`} key={index}>
          {index > 0 ? (
            <select
              className="logical-operator"
              aria-label={`Logical operator before filter ${index + 1}`}
              value={filter.logicalOperator ?? 0}
              onChange={(event) => updateFilter(index, "logicalOperator", Number(event.target.value))}
            >
              <option value={0}>AND</option>
              <option value={1}>OR</option>
            </select>
          ) : (
            <span className="logical-operator-spacer" aria-hidden="true" />
          )}
          <FieldBuilder
            entity={entity}
            metadata={metadata}
            value={filter.field}
            showPath
            showDataType
            ariaLabel="Filter field"
            onChange={(field) => updateFilter(index, "field", field)}
            onRemove={() => updateFilter(index, "field", "")}
          />
          <select value={filter.operator} onChange={(event) => updateFilter(index, "operator", Number(event.target.value))}>{operators.map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select>
          <input value={filter.value} placeholder="Value" onChange={(event) => updateFilter(index, "value", event.target.value)} />
          <button
            className="filter-toggle"
            type="button"
            aria-label={filter.enabled !== false ? "Disable filter" : "Enable filter"}
            aria-pressed={filter.enabled !== false}
            title={filter.enabled !== false ? "Disable filter" : "Enable filter"}
            onClick={() => updateFilter(index, "enabled", filter.enabled === false)}
          >
            {filter.enabled !== false ? "ON" : "OFF"}
          </button>
          <button className="icon-button" aria-label="Remove filter" onClick={() => onDefinitionChange({ ...definition, filters: definition.filters.filter((_, filterIndex) => filterIndex !== index) })}><Icon name="trash" size={13} /></button>
        </div>
      ))}
      {!definition.filters.length && <p className="empty-builder">No filters. All records will be considered.</p>}
    </div>
  );
}
