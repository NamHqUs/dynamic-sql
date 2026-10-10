import { useState } from "react";
import { MetadataEntity } from "../query-builder-slice";
import "./field-box.css";

type FieldBoxProps = {
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  value: string;
  onChange: (path: string) => void;
  clearOnSelect?: boolean;
  placeholder?: string;
  ariaLabel?: string;
  excludedPaths?: string[];
};

const RELATION_PREFIX = "relation:";
const FIELD_PREFIX = "field:";

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

export default function FieldBox({ entity, metadata, value, onChange, clearOnSelect = false, placeholder = "Select field…", ariaLabel = "Field", excludedPaths = [] }: FieldBoxProps) {
  const valueSegments = value ? value.split(".") : [];
  const [pendingPath, setPendingPath] = useState<string[] | null>(null);
  const path = pendingPath ?? valueSegments.slice(0, -1);
  const current = resolveEntity(entity, metadata, path) ?? entity;
  const selectedField = path.join(".") === valueSegments.slice(0, -1).join(".")
    ? valueSegments[valueSegments.length - 1] ?? ""
    : "";
  const selectValue = selectedField && current.fields.some((field) => field.name === selectedField)
    ? `${FIELD_PREFIX}${selectedField}`
    : "";

  const handleSelect = (selected: string) => {
    if (selected.startsWith(RELATION_PREFIX)) {
      setPendingPath([...path, selected.slice(RELATION_PREFIX.length)]);
      return;
    }
    if (!selected.startsWith(FIELD_PREFIX)) return;
    onChange([...path, selected.slice(FIELD_PREFIX.length)].join("."));
    setPendingPath(clearOnSelect ? [] : path);
  };

  const relations = (current.relations || []).filter((relation) => metadata.some((item) => item.name === relation.relatedEntity));

  return <div className="field-box">
    {path.map((segment, index) => <span className="field-box-crumb" key={`${index}-${segment}`}>
      <button type="button" className="field-box-segment" title={`Back to ${index ? path[index - 1] : entity.name}`} onClick={() => setPendingPath(path.slice(0, index))}>{segment}</button>
      <span className="field-box-dot">.</span>
    </span>)}
    <select aria-label={ariaLabel} value={selectValue} onChange={(event) => handleSelect(event.target.value)}>
      <option value="" disabled>{placeholder}</option>
      {relations.length > 0 && <optgroup label="Relations">
        {relations.map((relation) => <option value={`${RELATION_PREFIX}${relation.name}`} key={relation.name}>{relation.name}</option>)}
      </optgroup>}
      <optgroup label={`${current.name} fields`}>
        {current.fields
          .filter((field) => !excludedPaths.includes([...path, field.name].join(".")))
          .map((field) => <option value={`${FIELD_PREFIX}${field.name}`} key={field.name}>{field.name}</option>)}
      </optgroup>
    </select>
  </div>;
}
