import { createPortal } from "react-dom";
import { useEffect, useLayoutEffect, useRef, useState, type FormEvent } from "react";
import { ApiQueryContextDefinition, FilterOperatorDefinition, MetadataEntity, QueryDefinition } from "./query-builder-slice";
import { FunctionDefinition } from "../../metadata-builder/function-definition/types";
import Icon from "../../icon/icon";
import ProjectionBuilder from "./projection-builder/projection-builder";
import SortBuilder from "./sort-builder/sort-builder";
import FilterBuilder from "./filter-builder/filter-builder";
import JoinsBuilder from "./joins-builder/joins-builder";
import "./query-design.css";

type EntityDropdownProps = {
  entities: MetadataEntity[];
  value: string;
  disabled: boolean;
  onChange: (entity: MetadataEntity) => void;
};

function EntityDropdown({ entities, value, disabled, onChange }: EntityDropdownProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [menuPosition, setMenuPosition] = useState<{ top: number; left: number; width: number } | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const selectedEntity = entities.find((item) => item.name === value);
  const filteredEntities = entities.filter((item) => item.name.toLowerCase().includes(search.trim().toLowerCase()));

  useLayoutEffect(() => {
    if (!open) {
      setMenuPosition(null);
      return;
    }

    const updatePosition = () => {
      const trigger = triggerRef.current;
      const menu = menuRef.current;
      if (!trigger || !menu) return;
      const bounds = trigger.getBoundingClientRect();
      const menuHeight = menu.getBoundingClientRect().height;
      const top = window.innerHeight - bounds.bottom < menuHeight + 4 && bounds.top >= menuHeight + 4
        ? bounds.top - menuHeight - 4
        : bounds.bottom + 4;
      const width = Math.max(bounds.width, 240);
      const left = Math.min(bounds.left, Math.max(8, window.innerWidth - width - 8));
      setMenuPosition({ top, left, width });
    };

    updatePosition();
    window.addEventListener("resize", updatePosition);
    window.addEventListener("scroll", updatePosition, true);
    return () => {
      window.removeEventListener("resize", updatePosition);
      window.removeEventListener("scroll", updatePosition, true);
    };
  }, [open, search, filteredEntities.length]);

  useEffect(() => {
    if (!open) return;
    const closeOnOutsideClick = (event: MouseEvent) => {
      if (
        event.target instanceof Node
        && !containerRef.current?.contains(event.target)
        && !menuRef.current?.contains(event.target)
      ) setOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
        triggerRef.current?.focus();
      }
    };
    document.addEventListener("mousedown", closeOnOutsideClick);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("mousedown", closeOnOutsideClick);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [open]);

  return (
    <div className="entity-dropdown" ref={containerRef}>
      <button
        type="button"
        className={`entity-dropdown-trigger${selectedEntity ? "" : " empty"}`}
        ref={triggerRef}
        aria-label="Entity"
        aria-expanded={open}
        aria-haspopup="listbox"
        disabled={disabled}
        onClick={() => {
          setSearch("");
          setOpen((isOpen) => !isOpen);
        }}
      >
        <span>{selectedEntity?.name ?? (value || "Select an entity")}</span>
      </button>
      {open && createPortal(
        <div
          className="entity-dropdown-menu"
          ref={menuRef}
          role="listbox"
          aria-label="Entities"
          style={{
            position: "fixed",
            top: menuPosition?.top ?? 0,
            left: menuPosition?.left ?? 0,
            width: menuPosition?.width,
            visibility: menuPosition ? "visible" : "hidden",
          }}
        >
          <input
            ref={searchRef}
            className="entity-dropdown-search"
            type="search"
            aria-label="Search entities"
            placeholder="Search entities..."
            autoFocus
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
          {filteredEntities.length
            ? filteredEntities.map((item) => (
              <button
                type="button"
                className={`entity-dropdown-option${item.name === value ? " selected" : ""}`}
                role="option"
                aria-selected={item.name === value}
                key={item.name}
                onClick={() => {
                  onChange(item);
                  setOpen(false);
                  setSearch("");
                  triggerRef.current?.focus();
                }}
              >
                {item.name}
              </button>
            ))
            : <span className="entity-dropdown-empty" role="status">No entities found.</span>}
        </div>,
        document.body,
      )}
    </div>
  );
}

type QueryDesignProps = {
  metadata: MetadataEntity[];
  metadataStatus: string;
  metadataError: string;
  functionDefinitions: FunctionDefinition[];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  filterOperators: FilterOperatorDefinition[];
  filterOperatorsStatus: string;
  filterOperatorsError: string;
  definition: QueryDefinition;
  contextDefinitions: ApiQueryContextDefinition[];
  selectedContextIndex: number | null;
  onSelectContext: (index: number | null) => void;
  onCreateContext: (name: string) => boolean;
  onDeleteContext: (index: number) => void;
  onContextNameChange: (name: string) => void;
  entity?: MetadataEntity;
  requestName: string;
  onRequestNameChange: (name: string) => void;
  onDefinitionChange: (definition: QueryDefinition) => void;
};

export default function QueryDesign({
  metadata,
  metadataStatus,
  metadataError,
  functionDefinitions,
  functionDefinitionsStatus,
  functionDefinitionsError,
  filterOperators,
  filterOperatorsStatus,
  filterOperatorsError,
  definition,
  contextDefinitions,
  selectedContextIndex,
  onSelectContext,
  onCreateContext,
  onDeleteContext,
  onContextNameChange,
  entity,
  requestName,
  onRequestNameChange,
  onDefinitionChange,
}: QueryDesignProps) {
  const [draggedProjectionField, setDraggedProjectionField] = useState<string | null>(null);
  const [contextDialogOpen, setContextDialogOpen] = useState(false);
  const [newContextName, setNewContextName] = useState("");
  const [contextNameError, setContextNameError] = useState("");
  const submitContext = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const name = newContextName.trim();
    if (!name) {
      setContextNameError("Enter a name for the context.");
      return;
    }
    if (!onCreateContext(name)) {
      setContextNameError("A context with this name already exists.");
      return;
    }
    setContextDialogOpen(false);
    setNewContextName("");
    setContextNameError("");
  };

  return (
    <section className="query-builder-card">
      {contextDefinitions.length > 0 && <nav className="query-context-bar" aria-label="Query contexts">
        <button
          type="button"
          className={`query-context-tab${selectedContextIndex === null ? " selected" : ""}`}
          aria-pressed={selectedContextIndex === null}
          onClick={() => onSelectContext(null)}
        >
          <Icon name="database" size={14} /> MainQuery
        </button>
        {contextDefinitions.map((context, index) => <div className="query-context-tab-wrap" key={`${context.id ?? context.name}-${index}`}>
          <button
            type="button"
            className={`query-context-tab${selectedContextIndex === index ? " selected" : ""}`}
            aria-pressed={selectedContextIndex === index}
            onClick={() => onSelectContext(index)}
          >
            <Icon name="database" size={14} /> {context.name}
          </button>
          <button type="button" className="query-context-delete" aria-label={`Delete ${context.name} context`} title={`Delete ${context.name} context`} onClick={() => onDeleteContext(index)}>
            <Icon name="close" size={12} />
          </button>
        </div>)}
      </nav>}
      <div className="query-name-row">
        <div className="query-name-field">
          <div className="query-name-label-row">
            <span className="section-kicker">{selectedContextIndex === null ? "QUERY NAME" : "CONTEXT NAME"}</span>
            {selectedContextIndex === null && <button className="field-action field-action-icon query-create-context" type="button" aria-label="Create context" title="Create context" onClick={() => {
              setContextDialogOpen(true);
              setContextNameError("");
            }}>
              <Icon name="plus" size={14} />
            </button>}
          </div>
          <input
            type="text"
            value={selectedContextIndex === null ? requestName : contextDefinitions[selectedContextIndex]?.name ?? ""}
            onChange={(event) => selectedContextIndex === null
              ? onRequestNameChange(event.target.value)
              : onContextNameChange(event.target.value)}
            placeholder={selectedContextIndex === null ? "e.g. recent-invoices" : "Context name"}
            aria-label={selectedContextIndex === null ? "Query name" : "Context name"}
          />
        </div>
      </div>
      <div className="query-builder-toolbar">
        <label><span className="section-kicker">ENTITY</span>
          <EntityDropdown entities={metadata} value={definition.entity} disabled={!metadata.length} onChange={(selectedEntity) => {
            onDefinitionChange({
              entity: selectedEntity.name,
              columns: selectedEntity.fields.map((field) => ({ field: field.name, alias: null })),
              contexts: definition.contexts,
              contextCatalog: definition.contextCatalog,
              filters: { logicalOperation: 0, filters: [] },
              sorts: [],
              page: 1,
              pageSize: 50,
            });
          }} />
        </label>
        <div className="query-paging-controls">
          <label><span className="section-kicker">PAGE</span>
            <input className="small-input" type="number" min="1" step="1" value={definition.page ?? 1} disabled={(definition.page === null && definition.pageSize === null) || (definition.pageSize !== null && definition.pageSize < 1)} onChange={(event) => {
              const value = event.target.value;
              const page = value ? Number.parseInt(value, 10) : null;
              onDefinitionChange({ ...definition, page: page === null ? null : Math.max(1, page) });
            }} />
          </label>
          <label><span className="section-kicker">PAGE SIZE</span>
            <input className="small-input" type="number" step="1" max="1000" value={definition.pageSize ?? ""} onChange={(event) => onDefinitionChange({ ...definition, pageSize: event.target.value ? Number(event.target.value) : null })} />
          </label>
        </div>
      </div>
      {contextDialogOpen && <div className="context-name-backdrop">
        <form className="context-name-dialog" onSubmit={submitContext}>
          <span className="section-kicker">NEW CONTEXT</span>
          <h3>Create a context</h3>
          <p>Give this context a name. You can choose its entity and add joins afterward.</p>
          <label htmlFor="new-context-name">Context name</label>
          <input
            id="new-context-name"
            autoFocus
            value={newContextName}
            onChange={(event) => {
              setNewContextName(event.target.value);
              setContextNameError("");
            }}
            placeholder="e.g. InvoiceDetails"
            aria-invalid={Boolean(contextNameError)}
            aria-describedby={contextNameError ? "new-context-name-error" : undefined}
          />
          {contextNameError && <p className="context-name-error" id="new-context-name-error" role="alert">{contextNameError}</p>}
          <div className="context-name-actions">
            <button type="button" className="button secondary" onClick={() => {
              setContextDialogOpen(false);
              setNewContextName("");
              setContextNameError("");
            }}>Cancel</button>
            <button type="submit" className="button primary">Create Context</button>
          </div>
        </form>
      </div>}

      {metadataStatus === "loading" && <p className="query-message">Loading metadata…</p>}
      {metadataStatus === "failed" && <p className="query-message error" role="alert">{metadataError}</p>}
      {(selectedContextIndex === null && contextDefinitions.length > 0)
        || (selectedContextIndex !== null && contextDefinitions.length > 1)
        ? <JoinsBuilder
          definition={definition}
          contexts={contextDefinitions}
          metadata={metadata}
          operators={filterOperators}
          functionDefinitions={functionDefinitions}
          functionDefinitionsStatus={functionDefinitionsStatus}
          functionDefinitionsError={functionDefinitionsError}
          canAddJoin={contextDefinitions.length > 0}
          excludeContextId={selectedContextIndex === null ? undefined : contextDefinitions[selectedContextIndex]?.id}
          onDefinitionChange={onDefinitionChange}
        />
        : null}
      {entity && <div className="query-builder-body">
        <ProjectionBuilder
          metadata={metadata}
          functionDefinitions={functionDefinitions}
          functionDefinitionsStatus={functionDefinitionsStatus}
          functionDefinitionsError={functionDefinitionsError}
          definition={definition}
          entity={entity}
          onDefinitionChange={onDefinitionChange}
          onFieldDragStart={setDraggedProjectionField}
        />
        <FilterBuilder
          metadata={metadata}
          functionDefinitions={functionDefinitions}
          functionDefinitionsStatus={functionDefinitionsStatus}
          functionDefinitionsError={functionDefinitionsError}
          operators={filterOperators}
          operatorsStatus={filterOperatorsStatus}
          operatorsError={filterOperatorsError}
          definition={definition}
          entity={entity}
          onDefinitionChange={onDefinitionChange}
        />
        <SortBuilder
          metadata={metadata}
          functionDefinitions={functionDefinitions}
          functionDefinitionsStatus={functionDefinitionsStatus}
          functionDefinitionsError={functionDefinitionsError}
          definition={definition}
          entity={entity}
          onDefinitionChange={onDefinitionChange}
          draggedProjectionField={draggedProjectionField}
        />
      </div>}
    </section>
  );
}
