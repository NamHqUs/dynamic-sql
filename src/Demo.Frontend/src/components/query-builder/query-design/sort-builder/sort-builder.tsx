import { type DragEvent, useEffect, useRef, useState } from "react";
import { MetadataEntity, QueryDefinition } from "../query-builder-slice";
import FieldBuilder from "../field-builder/field-builder";
import Icon from "../../../icon/icon";

const QUERY_FIELD_DRAG_TYPE = "application/x-query-field";

type SortBuilderProps = {
  metadata: MetadataEntity[];
  definition: QueryDefinition;
  entity: MetadataEntity;
  onDefinitionChange: (definition: QueryDefinition) => void;
  draggedProjectionField: string | null;
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

export default function SortBuilder({ metadata, definition, entity, onDefinitionChange, draggedProjectionField }: SortBuilderProps) {
  const [editingField, setEditingField] = useState<string | null>(null);
  const [isDropTarget, setIsDropTarget] = useState(false);

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

  const clearSorts = () => {
    if (definition.sorts.length) onDefinitionChange({ ...definition, sorts: [] });
  };

  const getProjectField = (event: DragEvent<HTMLElement>) => {
    const field = (
      event.dataTransfer.getData(QUERY_FIELD_DRAG_TYPE)
      || event.dataTransfer.getData("text/plain")
    ).trim();
    return definition.columns.some((column) => column.field === field) ? field : undefined;
  };

  const isProjectFieldDrag = (event: DragEvent<HTMLElement>) =>
    Array.from(event.dataTransfer.types).includes(QUERY_FIELD_DRAG_TYPE);

  return (
    <div className="builder-section">
      <div className="section-header">
        <div className="section-heading">
          <span className="section-kicker">SORTS</span>
          <p className="filter-description">Select fields to sort the query results and choose the direction for each field.</p>
        </div>
        <button
          type="button"
          className="field-action field-action-clear"
          onClick={clearSorts}
          disabled={!definition.sorts.length}
        >
          - Clear sorts
        </button>
      </div>
      <div
        className={`sort-list${isDropTarget ? " sort-list-drop-target" : ""}`}
        onDragOver={(event) => {
          if (draggedProjectionField || isProjectFieldDrag(event)) {
            event.preventDefault();
            event.dataTransfer.dropEffect = "move";
            setIsDropTarget(true);
          }
        }}
        onDragLeave={(event) => {
          if (!(event.relatedTarget instanceof Node) || !event.currentTarget.contains(event.relatedTarget)) setIsDropTarget(false);
        }}
        onDrop={(event) => {
          const field = draggedProjectionField ?? getProjectField(event);
          if (!field) return;
          event.preventDefault();
          addSort(field);
          setIsDropTarget(false);
        }}
      >
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
            placeholder="+ Sort by field"
            ariaLabel="Sort by field"
            excludedPaths={definition.sorts.map((sort) => sort.field)}
            onChange={addSort}
          />
        </div>
      </div>
    </div>
  );
}
