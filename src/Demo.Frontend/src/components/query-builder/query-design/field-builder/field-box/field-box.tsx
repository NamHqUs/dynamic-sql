import { useEffect, useId, useLayoutEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { MetadataEntity } from "../query-builder-slice";
import Icon from "../../../../icon/icon";
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
  placeholderIcon?: boolean;
  ariaLabel?: string;
  excludedPaths?: string[];
  autoFocus?: boolean;
  allowAggregates?: boolean;
  allowValueSelection?: boolean;
  allowCalculationSelection?: boolean;
  initialSection?: "fields" | "value" | "calculation";
  initialConstantValue?: string | number;
  onValueSelect?: (value: string | number) => void;
  renderCalculation?: (onComplete: () => void) => ReactNode;
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

export default function FieldBox({
  entity,
  metadata,
  value,
  onChange,
  onIncompleteChange,
  clearOnSelect = false,
  completeOnSelect = false,
  showPath = false,
  showDataType = false,
  placeholder = "Select field…",
  placeholderIcon = false,
  ariaLabel = "Field",
  excludedPaths = [],
  autoFocus = false,
  allowAggregates = true,
  allowValueSelection = false,
  allowCalculationSelection = false,
  initialSection = "fields",
  initialConstantValue,
  onValueSelect,
  renderCalculation,
}: FieldBoxProps) {
  const valueSegments = value ? value.split(".") : [];
  const [pendingPath, setPendingPath] = useState<string[] | null>(null);
  const [pendingAggregate, setPendingAggregate] = useState<string | null>(null);
  const [pendingAggregateWrapsPath, setPendingAggregateWrapsPath] = useState<boolean | null>(null);
  const [pendingAggregateField, setPendingAggregateField] = useState("");
  const [menuOpen, setMenuOpen] = useState(false);
  const [fieldSearch, setFieldSearch] = useState("");
  const [constantValue, setConstantValue] = useState(String(initialConstantValue ?? ""));
  const [activeSection, setActiveSection] = useState<"fields" | "value" | "calculation">(initialSection);
  const constantValueInputId = useId();
  const menuRef = useRef<HTMLDivElement>(null);
  const menuPopupRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const [menuPosition, setMenuPosition] = useState({ top: 0, left: 0, maxHeight: 420 });

  useEffect(() => {
    if (autoFocus) setMenuOpen(true);
  }, [autoFocus]);

  useLayoutEffect(() => {
    if (!menuOpen || !triggerRef.current) return;
    const updateMenuPosition = () => {
      if (!triggerRef.current) return;
      const anchor = triggerRef.current.closest(".field-builder-editing") ?? triggerRef.current;
      const bounds = anchor.getBoundingClientRect();
      const menu = menuPopupRef.current;
      const menuHeight = menu
        ? Math.max(menu.getBoundingClientRect().height, menu.scrollHeight)
        : 420;
      const menuWidth = menu?.getBoundingClientRect().width ?? 200;
      const inFieldCard = Boolean(triggerRef.current.closest(".field-card"));
      const inProjectionFieldPicker = Boolean(triggerRef.current.closest(".projection-row-builder"));
      const inSelectedFieldCard = Boolean(triggerRef.current.closest(".field-card.selected"));
      const projectionBounds = inProjectionFieldPicker
        ? triggerRef.current.closest(".projection-section")?.getBoundingClientRect()
        : inSelectedFieldCard
          ? triggerRef.current.closest(".field-card.selected")?.getBoundingClientRect()
          : undefined;
      const positionBounds = projectionBounds ?? bounds;
      const openBelow = inProjectionFieldPicker || inSelectedFieldCard;
      const spaceBelow = window.innerHeight - positionBounds.bottom;
      const openAbove = !openBelow && (inFieldCard
        ? spaceBelow < Math.min(menuHeight, 180) + 4 && bounds.top > spaceBelow
        : spaceBelow < menuHeight + 4 && bounds.top >= menuHeight + 4);
      const availableHeight = openAbove ? positionBounds.top - 8 : spaceBelow - 8;
      const top = openAbove
        ? positionBounds.top - Math.min(menuHeight, availableHeight) - 4
        : positionBounds.bottom + (openBelow ? 0 : 4);
      const left = Math.min(positionBounds.left, Math.max(4, window.innerWidth - menuWidth - 4));
      setMenuPosition({
        top,
        left,
        maxHeight: inFieldCard || inProjectionFieldPicker
          ? Math.max(0, Math.min(menuHeight, availableHeight))
          : 420,
      });
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
  useEffect(() => {
    if (!menuOpen) setFieldSearch("");
  }, [menuOpen]);
  const rootAggregateMatch = value.match(/^(Count\(\)|Sum|Max|Min|Average)\(([^()]*)\)$/);
  const legacyAggregateMatch = valueSegments[valueSegments.length - 1]?.match(/^(Count\(\)|Sum|Max|Min|Average)\(([^)]*)\)$/);
  const aggregateMatch = rootAggregateMatch ?? legacyAggregateMatch;
  const aggregateWrapsPath = pendingAggregateWrapsPath ?? Boolean(rootAggregateMatch);
  const aggregateFieldPath = rootAggregateMatch
    ? rootAggregateMatch[2].split(".")
    : valueSegments.slice(-1)[0]?.match(/^(?:Count\(\)|Sum|Max|Min|Average)\(([^)]*)\)$/)?.[1]?.split(".");
  const rawPath = pendingPath ?? (
    rootAggregateMatch
      ? aggregateFieldPath?.slice(0, -1) ?? []
      : valueSegments.slice(0, -1)
  );
  const aggregatePathIndex = rawPath.findIndex((segment) => aggregateSegment(segment));
  const path = aggregatePathIndex < 0 ? rawPath : rawPath.slice(0, aggregatePathIndex);
  const current = resolveEntity(entity, metadata, path) ?? entity;
  const selectedField = aggregateMatch
    ? aggregateFieldPath?.[aggregateFieldPath.length - 1] ?? ""
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

  const handleSelect = (selected: string) => {
    setFieldSearch("");
    if (selected.startsWith(RELATION_PREFIX)) {
      setPendingPath([...path, selected.slice(RELATION_PREFIX.length)]);
      return;
    }
    if (selected === `${AGGREGATE_PREFIX}${COUNT_AGGREGATE}`) {
      onChange([...path, COUNT_AGGREGATE].join("."));
      setPendingAggregate(null);
      setPendingAggregateWrapsPath(null);
      setPendingAggregateField("");
      setPendingPath(clearOnSelect ? [] : path);
      setMenuOpen(false);
      return;
    }
    if (selected.startsWith(AGGREGATE_PREFIX)) {
      setPendingAggregate(selected.slice(AGGREGATE_PREFIX.length));
      setPendingAggregateWrapsPath(path.length === 0);
      setPendingAggregateField("");
      return;
    }
    if (!selected.startsWith(FIELD_PREFIX)) return;
    const field = selected.slice(FIELD_PREFIX.length);
    const activeAggregate = pendingAggregate ?? selectedAggregate;
    if (activeAggregate && activeAggregate !== COUNT_AGGREGATE) {
      selectAggregateField(field);
      setMenuOpen(false);
      return;
    }
    onChange([...path, field].join("."));
    setPendingPath(clearOnSelect ? [] : path);
    setMenuOpen(false);
  };

  const relations = (current.relations || []).filter((relation) => metadata.some((item) => item.name === relation.relatedEntity));
  const activeAggregate = pendingAggregate ?? selectedAggregate;
  const fields = current.fields.filter((field) =>
    (!activeAggregate || activeAggregate === COUNT_AGGREGATE || NUMERIC_DATA_TYPES.has(field.dataType.toLowerCase())) &&
    !excludedPaths.includes([...path, field.name].join(".")));
  const filteredFields = fields.filter((field) => field.name.toLowerCase().includes(fieldSearch.trim().toLowerCase()));
  const fieldColumns = useMemo(() => {
    if (filteredFields.length === 0) return [];
    if (filteredFields.length <= 5) return [filteredFields];

    const canvas = document.createElement("canvas");
    const context = canvas.getContext("2d");
    if (context) context.font = '12px "DM Mono", monospace';
    const measuredFields = filteredFields
      .map((field, index) => ({
        field,
        index,
        width: context?.measureText(field.name).width ?? field.name.length,
      }))
      .sort((left, right) => left.width - right.width || left.index - right.index);
    const splitIndex = Math.ceil(measuredFields.length / 2);

    return [measuredFields.slice(0, splitIndex), measuredFields.slice(splitIndex)]
      .map((column) => column
        .sort((left, right) => left.index - right.index)
        .map(({ field }) => field));
  }, [filteredFields]);
  const isComplete = completeOnSelect && Boolean(value);
  const selectedDataType = aggregateMatch || selectedAggregate
    ? "Number"
    : current.fields.find((field) => field.name === selectedField)?.dataType;
  const selectAggregateField = (field: string) => {
    setPendingAggregateField(field);
    if (activeAggregate && activeAggregate !== COUNT_AGGREGATE && field) {
      const aggregate = `${activeAggregate}(${field})`;
      onChange(aggregateWrapsPath ? `${activeAggregate}(${[...path, field].join(".")})` : [...path, aggregate].join("."));
    }
  };
  const selectConstantValue = () => {
    const numberValue = Number(constantValue);
    const selectedValue = initialConstantValue !== undefined
      && String(initialConstantValue) === constantValue
      ? initialConstantValue
      : constantValue.trim() && Number.isFinite(numberValue)
        ? numberValue
        : constantValue;
    onValueSelect?.(selectedValue);
    setMenuOpen(false);
  };
  const hasAdditionalSections = allowValueSelection || allowCalculationSelection;

  return <div className="field-box">
    {!isComplete && aggregateWrapsPath && activeAggregate && activeAggregate !== COUNT_AGGREGATE &&
      <span className="field-box-aggregate-label">{activeAggregate}(</span>}
    {!isComplete && !showPath && path.map((segment, index) => <span className="field-box-crumb" key={`${index}-${segment}`}>
      <button type="button" className="field-box-segment" title={`Back to ${index ? path[index - 1] : entity.name}`} onClick={() => setPendingPath(path.slice(0, index))}>{segment}</button>
      <span className="field-box-dot">.</span>
    </span>)}
    {!isComplete && !aggregateWrapsPath && activeAggregate && activeAggregate !== COUNT_AGGREGATE &&
      <span className="field-box-aggregate-label">{activeAggregate}(</span>}
    {!isComplete && <div className={`field-box-menu${value ? " field-box-menu-selected" : ""}`} ref={menuOpen ? menuRef : undefined}>
      <button
        type="button"
        autoFocus={autoFocus}
        ref={triggerRef}
        className="operator-trigger"
        aria-label={ariaLabel}
        aria-expanded={menuOpen}
        aria-haspopup="menu"
        onClick={() => setMenuOpen((open) => !open)}
      >
        {(() => {
          const label = showPath && (pendingPath?.length ? pendingPath.join(".") : value)
            ? (pendingPath?.length ? pendingPath.join(".") : value)
            : selectedField || (activeAggregate && activeAggregate !== COUNT_AGGREGATE ? placeholder : selectedAggregate) || placeholder;
          return placeholderIcon && label === placeholder
            ? <><Icon name="plus" size={12} /><span className="action-link-text">{label}</span></>
            : label;
        })()}
      </button>
      {menuOpen && createPortal(<div
        ref={menuPopupRef}
        className={`operator-menu field-box-menu-popup${hasAdditionalSections ? " field-box-menu-popup-advanced" : ""}${activeSection === "fields" ? " field-box-menu-popup-fields" : ""}${activeSection === "value" ? " field-box-menu-popup-value" : ""}${activeSection === "calculation" && renderCalculation ? " field-box-menu-popup-calculation" : ""}`}
        style={{ position: "fixed", top: menuPosition.top, left: menuPosition.left, maxHeight: hasAdditionalSections ? "none" : menuPosition.maxHeight, transform: "none", margin: 0 }}
        onMouseDown={(event) => {
          event.preventDefault();
          event.stopPropagation();
        }}
        role="group"
        aria-label={`${ariaLabel} options`}
      >
        {hasAdditionalSections && <div className="field-box-chooser-tabs" role="tablist" aria-label="Projection field type">
          <button
            type="button"
            role="tab"
            aria-selected={activeSection === "fields"}
            className={`field-box-chooser-tab${activeSection === "fields" ? " active" : ""}`}
            onClick={() => setActiveSection("fields")}
          >Fields</button>
          {allowValueSelection && <button
            type="button"
            role="tab"
            aria-selected={activeSection === "value"}
            className={`field-box-chooser-tab${activeSection === "value" ? " active" : ""}`}
            onClick={() => setActiveSection("value")}
          >Value</button>}
          {allowCalculationSelection && <button
            type="button"
            role="tab"
            aria-selected={activeSection === "calculation"}
            className={`field-box-chooser-tab${activeSection === "calculation" ? " active" : ""}`}
            onClick={() => setActiveSection("calculation")}
          >Calculation</button>}
        </div>}
        {activeSection === "value" && allowValueSelection
          ? <div className="field-box-value-panel" role="tabpanel" aria-label="Constant value">
            <label className="field-box-value-label" htmlFor={constantValueInputId}>Constant value</label>
            <input
                id={constantValueInputId}
              type="text"
              className="field-box-search field-box-value-input"
              aria-label="Constant value"
              placeholder="Enter text or a number"
              autoFocus
              value={constantValue}
              onChange={(event) => setConstantValue(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault();
                  selectConstantValue();
                }
              }}
            />
            <button type="button" className="field-box-value-submit" onClick={selectConstantValue}><Icon name="check" size={12} /> Apply</button>
          </div>
          : activeSection === "calculation" && allowCalculationSelection
            ? <div className={`field-box-value-panel field-box-calculation-panel${renderCalculation ? " field-box-calculation-panel-expanded" : ""}`} role="tabpanel" aria-label="Calculation">
              {renderCalculation
                ? renderCalculation(() => setMenuOpen(false))
                : <span className="field-box-calculation-hint">Build an expression from fields, SQL functions, and values.</span>}
            </div>
            : <div role={hasAdditionalSections ? "tabpanel" : undefined} aria-label={hasAdditionalSections ? "Fields" : undefined}>
              <input
                type="search"
                className="field-box-search"
                aria-label="Search fields"
                placeholder="Search fields…"
                autoFocus
                value={fieldSearch}
                onChange={(event) => setFieldSearch(event.target.value)}
              />
              <div role="listbox" aria-label="Available fields">
                {relations.length > 0 && <>
                  <span className="field-box-menu-label">Relations</span>
                  {relations.map((relation) => (
                    <button type="button" className="operator-option" role="option" key={relation.name} onClick={() => handleSelect(`${RELATION_PREFIX}${relation.name}`)}>
                      {relation.name}
                    </button>
                  ))}
                </>}
                <span className="field-box-menu-label">{current.name} fields</span>
                {fieldColumns.length > 0 && <div
                  className="field-box-field-grid"
                  style={{ gridTemplateColumns: `repeat(${fieldColumns.length}, max-content)` }}
                >
                  {fieldColumns.map((column, columnIndex) => <div className="field-box-field-column" key={columnIndex}>
                    {column.map((field) => (
                      <button type="button" className={`operator-option${selectValue === `${FIELD_PREFIX}${field.name}` ? " operator-option-selected" : ""}`} role="option" aria-selected={selectValue === `${FIELD_PREFIX}${field.name}`} key={field.name} onClick={() => handleSelect(`${FIELD_PREFIX}${field.name}`)}>
                        {field.name}
                      </button>
                    ))}
                  </div>)}
                </div>}
                {filteredFields.length === 0 && <span className="field-box-no-results">No matching fields</span>}
                {!activeAggregate && allowAggregates && <>
                  <span className="field-box-menu-label">Aggregate</span>
                  {AGGREGATES.map((aggregate) => (
                    <button type="button" className="operator-option" role="option" key={aggregate} onClick={() => handleSelect(`${AGGREGATE_PREFIX}${aggregate}`)}>
                      {aggregate === COUNT_AGGREGATE ? aggregate : `${aggregate}(…)`}
                    </button>
                  ))}
                </>}
              </div>
            </div>}
      </div>, document.body)}
    </div>}
    {completeOnSelect && value && <span className="field-box-completed" aria-label={`Selected ${ariaLabel.toLowerCase()}: ${value}`}>
      <span>{value}</span>
      {showDataType && selectedDataType && <span className="field-box-completed-type">{selectedDataType}</span>}
    </span>}
    {!isComplete && activeAggregate && activeAggregate !== COUNT_AGGREGATE &&
      <span className="field-box-aggregate-label">)</span>}
  </div>;
}
