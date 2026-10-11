import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { MetadataEntity } from "../query-builder-slice";
import "./field-box.css";

type FieldBoxProps = {
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  value: string;
  onChange: (path: string) => void;
  onIncompleteChange?: (incomplete: boolean) => void;
  clearOnSelect?: boolean;
  completeOnSelect?: boolean;
  showPath?: boolean;
  showDataType?: boolean;
  placeholder?: string;
  ariaLabel?: string;
  excludedPaths?: string[];
  autoFocus?: boolean;
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

export default function FieldBox({ entity, metadata, value, onChange, onIncompleteChange, clearOnSelect = false, completeOnSelect = false, showPath = false, showDataType = false, placeholder = "Select field…", ariaLabel = "Field", excludedPaths = [], autoFocus = false }: FieldBoxProps) {
  const valueSegments = value ? value.split(".") : [];
  const [pendingPath, setPendingPath] = useState<string[] | null>(null);
  const [pendingAggregate, setPendingAggregate] = useState<string | null>(null);
  const [pendingAggregateField, setPendingAggregateField] = useState("");
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const menuPopupRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const [menuPosition, setMenuPosition] = useState({ top: 0, left: 0 });

  useEffect(() => {
    if (autoFocus) setMenuOpen(true);
  }, [autoFocus]);

  useEffect(() => {
    if (!menuOpen || !triggerRef.current) return;
    const updateMenuPosition = () => {
      if (!triggerRef.current) return;
      const anchor = triggerRef.current.closest(".field-builder-editing") ?? triggerRef.current;
      const bounds = anchor.getBoundingClientRect();
      setMenuPosition({ top: bounds.bottom + 4, left: bounds.left });
    };

    updateMenuPosition();
    window.addEventListener("resize", updateMenuPosition);
    window.addEventListener("scroll", updateMenuPosition, true);
    return () => {
      window.removeEventListener("resize", updateMenuPosition);
      window.removeEventListener("scroll", updateMenuPosition, true);
    };
  }, [menuOpen]);

  useEffect(() => {
    if (!menuOpen) return;

    const closeOnOutsideClick = (event: MouseEvent) => {
      if (event.target instanceof Node
        && !menuRef.current?.contains(event.target)
        && !menuPopupRef.current?.contains(event.target)) setMenuOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setMenuOpen(false);
    };

    document.addEventListener("mousedown", closeOnOutsideClick);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("mousedown", closeOnOutsideClick);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [menuOpen]);
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
  useEffect(() => {
    onIncompleteChange?.(pendingPath !== null || (pendingAggregate !== null && !pendingAggregateField));
  }, [onIncompleteChange, pendingAggregate, pendingAggregateField, pendingPath]);
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
      setMenuOpen(false);
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
    setMenuOpen(false);
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
    {!isComplete && !showPath && !(isCollection && activeAggregate && activeAggregate !== COUNT_AGGREGATE) && path.map((segment, index) => <span className="field-box-crumb" key={`${index}-${segment}`}>
      <button type="button" className="field-box-segment" title={`Back to ${index ? path[index - 1] : entity.name}`} onClick={() => setPendingPath(path.slice(0, index))}>{segment}</button>
      <span className="field-box-dot">.</span>
    </span>)}
    {!isComplete && !(isCollection && activeAggregate && activeAggregate !== COUNT_AGGREGATE) && <div className={`field-box-menu${value ? " field-box-menu-selected" : ""}`} ref={menuOpen ? menuRef : undefined}>
      <button
        type="button"
        autoFocus={autoFocus}
        ref={triggerRef}
        className="operator-trigger"
        aria-label={ariaLabel}
        aria-expanded={menuOpen}
        aria-haspopup="listbox"
        onClick={() => setMenuOpen((open) => !open)}
      >
        {showPath && (pendingPath?.length ? pendingPath.join(".") : value)
          ? (pendingPath?.length ? pendingPath.join(".") : value)
          : selectedField || selectedAggregate || placeholder}
      </button>
      {menuOpen && createPortal(<div ref={menuPopupRef} className="operator-menu field-box-menu-popup" style={{ position: "fixed", top: menuPosition.top, left: menuPosition.left, transform: "none", margin: 0 }} onMouseDown={(event) => event.preventDefault()} role="listbox" aria-label={`${ariaLabel} options`}>
        {relations.length > 0 && <>
          <span className="field-box-menu-label">Relations</span>
          {relations.map((relation) => (
            <button type="button" className="operator-option" role="option" key={relation.name} onClick={() => handleSelect(`${RELATION_PREFIX}${relation.name}`)}>
              {relation.name}
            </button>
          ))}
        </>}
        <span className="field-box-menu-label">{current.name} fields</span>
        {current.fields
          .filter((field) => !excludedPaths.includes([...path, field.name].join(".")))
          .map((field) => (
            <button type="button" className={`operator-option${selectValue === `${FIELD_PREFIX}${field.name}` ? " operator-option-selected" : ""}`} role="option" aria-selected={selectValue === `${FIELD_PREFIX}${field.name}`} key={field.name} onClick={() => handleSelect(`${FIELD_PREFIX}${field.name}`)}>
              {field.name}
            </button>
          ))}
        {isCollection && <>
          <span className="field-box-menu-label">Aggregate</span>
          {AGGREGATES.map((aggregate) => (
            <button type="button" className="operator-option" role="option" key={aggregate} onClick={() => handleSelect(`${AGGREGATE_PREFIX}${aggregate}`)}>
              {aggregate === COUNT_AGGREGATE ? aggregate : `${aggregate}(…)`}
            </button>
          ))}
        </>}
      </div>, document.body)}
    </div>}
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
