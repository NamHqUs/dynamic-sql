import { useState } from "react";
import { MetadataEntity } from "../query-builder-slice";
import "./field-box.css";

type FieldBoxProps = {
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  value: string;
  onChange: (path: string) => void;
  clearOnSelect?: boolean;
  completeOnSelect?: boolean;
  showDataType?: boolean;
  placeholder?: string;
  ariaLabel?: string;
  excludedPaths?: string[];
};

const RELATION_PREFIX = "relation:";
const FIELD_PREFIX = "field:";
const AGGREGATE_PREFIX = "aggregate:";
const COUNT_AGGREGATE = "Count()";
const AGGREGATES = ["Count()", "Sum", "Max", "Min", "Average"] as const;
const NUMERIC_DATA_TYPES = new Set(["number", "byte", "short", "int", "integer", "long", "float", "double", "decimal"]);
const aggregateSegment = (segment: string) =>
  AGGREGATES.find((aggregate) => segment === aggregate || segment.startsWith(`${aggregate}(`));

// Follows relation names from the root entity; returns undefined when a segment cannot be resolved.
export function resolveEntity(root: MetadataEntity, metadata: MetadataEntity[], segments: string[]) {
  let current: MetadataEntity | undefined = root;
  for (const segment of segments) {
    const relation: { relatedEntity: string } | undefined = current?.relations?.find((item) => item.name === segment);
    current = relation && metadata.find((item) => item.name === relation.relatedEntity);
    if (!current) return undefined;
  }
  return current;
}

export default function FieldBox({ entity, metadata, value, onChange, clearOnSelect = false, completeOnSelect = false, showDataType = false, placeholder = "Select field…", ariaLabel = "Field", excludedPaths = [] }: FieldBoxProps) {
  const valueSegments = value ? value.split(".") : [];
  const [pendingPath, setPendingPath] = useState<string[] | null>(null);
  const [pendingAggregate, setPendingAggregate] = useState<string | null>(null);
  const [pendingAggregateField, setPendingAggregateField] = useState("");
  const aggregateMatch = valueSegments[valueSegments.length - 1]?.match(/^(Count\(\)|Sum|Max|Min|Average)\(([^)]*)\)$/);
  const rawPath = pendingPath ?? (aggregateMatch ? valueSegments.slice(0, -1) : valueSegments.slice(0, -1));
  const aggregatePathIndex = rawPath.findIndex((segment) => aggregateSegment(segment));
  const path = aggregatePathIndex < 0 ? rawPath : rawPath.slice(0, aggregatePathIndex);
  const current = resolveEntity(entity, metadata, path) ?? entity;
  const selectedField = aggregateMatch
    ? aggregateMatch[2]
    : path.join(".") === valueSegments.slice(0, -1).join(".")
      ? valueSegments[valueSegments.length - 1] ?? ""
      : "";
  const selectedAggregate = aggregateMatch?.[1] ?? pendingAggregate;
  const selectValue = selectedAggregate
      ? `${AGGREGATE_PREFIX}${selectedAggregate}`
      : selectedField && current.fields.some((field) => field.name === selectedField)
        ? `${FIELD_PREFIX}${selectedField}`
      : "";

  const parent = path.length > 0 ? resolveEntity(entity, metadata, path.slice(0, -1)) : undefined;
  const selectedRelation = parent?.relations?.find((relation) => relation.name === path[path.length - 1]);
  const isCollection = selectedRelation?.type === 1 || selectedRelation?.type === "Collection";

  const handleSelect = (selected: string) => {
    if (selected.startsWith(RELATION_PREFIX)) {
      setPendingPath([...path, selected.slice(RELATION_PREFIX.length)]);
      return;
    }
    if (selected === `${AGGREGATE_PREFIX}${COUNT_AGGREGATE}`) {
      onChange([...path, COUNT_AGGREGATE].join("."));
      setPendingAggregate(null);
      setPendingAggregateField("");
      setPendingPath(clearOnSelect ? [] : path);
      return;
    }
    if (selected.startsWith(AGGREGATE_PREFIX)) {
      setPendingAggregate(selected.slice(AGGREGATE_PREFIX.length));
      setPendingAggregateField("");
      return;
    }
    if (!selected.startsWith(FIELD_PREFIX)) return;
    onChange([...path, selected.slice(FIELD_PREFIX.length)].join("."));
    setPendingPath(clearOnSelect ? [] : path);
  };

  const relations = (current.relations || []).filter((relation) => metadata.some((item) => item.name === relation.relatedEntity));
  const numericFields = current.fields.filter((field) =>
    NUMERIC_DATA_TYPES.has(field.dataType.toLowerCase()) &&
    !excludedPaths.includes([...path, field.name].join(".")));
  const aggregateField = pendingAggregateField || selectedField;
  const activeAggregate = pendingAggregate ?? selectedAggregate;
  const isComplete = completeOnSelect && Boolean(value);
  const selectedDataType = aggregateMatch || selectedAggregate
    ? "Number"
    : current.fields.find((field) => field.name === selectedField)?.dataType;
  const selectAggregateField = (field: string) => {
    setPendingAggregateField(field);
    if (activeAggregate && activeAggregate !== COUNT_AGGREGATE && field)
      onChange([...path, `${activeAggregate}(${field})`].join("."));
  };

  return <div className="field-box">
    {!isComplete && !(isCollection && activeAggregate && activeAggregate !== COUNT_AGGREGATE) && path.map((segment, index) => <span className="field-box-crumb" key={`${index}-${segment}`}>
      <button type="button" className="field-box-segment" title={`Back to ${index ? path[index - 1] : entity.name}`} onClick={() => setPendingPath(path.slice(0, index))}>{segment}</button>
      <span className="field-box-dot">.</span>
    </span>)}
    {!isComplete && !(isCollection && activeAggregate && activeAggregate !== COUNT_AGGREGATE) && <select aria-label={ariaLabel} value={selectValue} onChange={(event) => handleSelect(event.target.value)}>
      <option value="" disabled>{placeholder}</option>
      {relations.length > 0 && <optgroup label="Relations">
        {relations.map((relation) => <option value={`${RELATION_PREFIX}${relation.name}`} key={relation.name}>{relation.name}</option>)}
      </optgroup>}
      <optgroup label={`${current.name} fields`}>
        {current.fields
          .filter((field) => !excludedPaths.includes([...path, field.name].join(".")))
          .map((field) => <option value={`${FIELD_PREFIX}${field.name}`} key={field.name}>{field.name}</option>)}
      </optgroup>
      {isCollection && <optgroup label="Aggregate">
        {AGGREGATES.map((aggregate) => <option value={`${AGGREGATE_PREFIX}${aggregate}`} key={aggregate}>
          {aggregate === COUNT_AGGREGATE ? aggregate : `${aggregate}(…)`}
        </option>)}
      </optgroup>}
    </select>}
    {completeOnSelect && value && <span className="field-box-completed" aria-label={`Selected ${ariaLabel.toLowerCase()}: ${value}`}>
      <span>{value}</span>
      {showDataType && selectedDataType && <span className="field-box-completed-type">{selectedDataType}</span>}
    </span>}
    {!isComplete && isCollection && activeAggregate && activeAggregate !== COUNT_AGGREGATE && <>
      <span className="field-box-aggregate-label">{[...path, activeAggregate].join(".")}</span>
      <select
        aria-label={`${activeAggregate} field`}
        value={aggregateField}
        onChange={(event) => selectAggregateField(event.target.value)}
      >
        <option value="" disabled>Select numeric field…</option>
        {numericFields.map((field) => <option value={field.name} key={field.name}>{field.name}</option>)}
      </select>
    </>}
  </div>;
}
