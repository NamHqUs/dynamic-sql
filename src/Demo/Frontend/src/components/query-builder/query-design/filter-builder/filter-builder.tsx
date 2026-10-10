import Icon from "../../../icon/icon";
import { MetadataEntity, QueryDefinition } from "../query-builder-slice";
import FieldBox from "../field-box/field-box";

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
  const updateFilter = (index: number, key: string, value: string | number) => {
    const filters = definition.filters.map((filter, filterIndex) => filterIndex === index ? { ...filter, [key]: value } : filter);
    onDefinitionChange({ ...definition, filters });
  };

  return (
    <div className="builder-section filters-section">
      <div className="section-header"><span className="section-kicker">FILTERS</span><button className="text-button" onClick={() => onDefinitionChange({ ...definition, filters: [...definition.filters, { field: entity.fields[0]?.name || "", operator: 0, value: "", parameterized: false }] })}><Icon name="plus" size={12} /> Add filter</button></div>
      {definition.filters.map((filter, index) => (
        <div className="filter-row" key={index}>
          <FieldBox key={entity.name} entity={entity} metadata={metadata} value={filter.field} ariaLabel="Filter field" onChange={(field) => updateFilter(index, "field", field)} />
          <select value={filter.operator} onChange={(event) => updateFilter(index, "operator", Number(event.target.value))}>{operators.map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select>
          <input value={filter.value} placeholder="Value" onChange={(event) => updateFilter(index, "value", event.target.value)} />
          <button className="icon-button" aria-label="Remove filter" onClick={() => onDefinitionChange({ ...definition, filters: definition.filters.filter((_, filterIndex) => filterIndex !== index) })}><Icon name="trash" size={13} /></button>
        </div>
      ))}
      {!definition.filters.length && <p className="empty-builder">No filters. All records will be considered.</p>}
    </div>
  );
}
