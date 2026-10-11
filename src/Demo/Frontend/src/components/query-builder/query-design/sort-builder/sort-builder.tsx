import { useEffect, useRef, useState } from "react";
import { MetadataEntity, QueryDefinition } from "../query-builder-slice";
import FieldBuilder from "../field-builder/field-builder";
import Icon from "../../../icon/icon";

type SortBuilderProps = {
  metadata: MetadataEntity[];
  definition: QueryDefinition;
  entity: MetadataEntity;
  onDefinitionChange: (definition: QueryDefinition) => void;
};

function SortDirectionMenu({
  value,
  field,
  onChange,
}: {
  value: string;
  field: string;
  onChange: (direction: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const label = value === "desc" ? "Descending" : "Ascending";

  useEffect(() => {
    if (!open) return;

    const closeOnOutsideClick = (event: MouseEvent) => {
      if (event.target instanceof Node && !containerRef.current?.contains(event.target)) setOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };

    document.addEventListener("mousedown", closeOnOutsideClick);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("mousedown", closeOnOutsideClick);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [open]);

  return (
    <div className="sort-direction-select" ref={containerRef}>
      <button
        type="button"
        className="operator-trigger"
        aria-label={`Sort direction for ${field}`}
        aria-expanded={open}
        aria-haspopup="listbox"
        onClick={() => setOpen((isOpen) => !isOpen)}
      >
        {label}
      </button>
      {open && (
        <div className="operator-menu" role="listbox" aria-label={`Sort direction options for ${field}`}>
          {[
            ["asc", "Ascending"],
            ["desc", "Descending"],
          ].map(([direction, directionLabel]) => (
            <button
              type="button"
              className={`operator-option${direction === value ? " operator-option-selected" : ""}`}
              role="option"
              aria-selected={direction === value}
              key={direction}
              onClick={() => {
                onChange(direction);
                setOpen(false);
              }}
            >
              {directionLabel}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

export default function SortBuilder({ metadata, definition, entity, onDefinitionChange }: SortBuilderProps) {
  const [editingField, setEditingField] = useState<string | null>(null);

  const addSort = (field: string) => {
    if (definition.sorts.some((sort) => sort.field === field)) return;
    onDefinitionChange({
      ...definition,
      sorts: [...definition.sorts, { field, direction: "asc" }],
    });
  };

  const updateSort = (field: string, direction: string) => {
    onDefinitionChange({
      ...definition,
      sorts: definition.sorts.map((sort) => sort.field === field ? { ...sort, direction } : sort),
    });
  };

  const replaceSort = (field: string, replacement: string) => {
    if (definition.sorts.some((sort) => sort.field === replacement)) return;
    onDefinitionChange({
      ...definition,
      sorts: definition.sorts.map((sort) => sort.field === field ? { ...sort, field: replacement } : sort),
    });
  };

  const removeSort = (field: string) => {
    onDefinitionChange({
      ...definition,
      sorts: definition.sorts.filter((sort) => sort.field !== field),
    });
  };

  return (
    <div className="builder-section">
      <div className="section-header">
        <div className="section-heading">
          <span className="section-kicker">SORTS</span>
          <p className="filter-description">Select fields to sort the query results and choose the direction for each field.</p>
        </div>
      </div>
      <div className="sort-list">
        {definition.sorts.map((sort) => (
          <div className="field-card selected" key={sort.field}>
            <button type="button" className="field-card-remove" onClick={() => removeSort(sort.field)} aria-label={`Remove sort for ${sort.field}`} title={`Remove sort for ${sort.field}`}>
              <Icon name="close" size={12} />
            </button>
            <div className="field-card-details">
              <div className="field-card-primary">
                {editingField === sort.field
                  ? <FieldBuilder
                    entity={entity}
                    metadata={metadata}
                    value={sort.field}
                    editMode
                    ariaLabel="Sort field"
                    excludedPaths={definition.sorts.filter((item) => item.field !== sort.field).map((item) => item.field)}
                    onChange={(replacement) => {
                      replaceSort(sort.field, replacement);
                      setEditingField(null);
                    }}
                    onCancel={() => setEditingField(null)}
                  />
                  : <span
                    className="field-name editable"
                    onClick={() => setEditingField(sort.field)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ") setEditingField(sort.field);
                    }}
                  >{sort.field}</span>}
                <span className="field-alias-separator" aria-hidden="true">|</span>
                <SortDirectionMenu
                  value={sort.direction}
                  field={sort.field}
                  onChange={(direction) => updateSort(sort.field, direction)}
                />
              </div>
            </div>
          </div>
        ))}
        <div className="sort-row-add">
          <FieldBuilder
            key={`add-sort-${definition.sorts.length}`}
            entity={entity}
            metadata={metadata}
            value=""
            placeholder="Add sort field"
            ariaLabel="Add sort field"
            excludedPaths={definition.sorts.map((sort) => sort.field)}
            onChange={addSort}
          />
        </div>
      </div>
    </div>
  );
}
