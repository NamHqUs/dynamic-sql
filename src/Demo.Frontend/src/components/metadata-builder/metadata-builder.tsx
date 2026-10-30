import { useCallback, useEffect, useRef, useState } from "react";
import { useDispatch } from "react-redux";
import { useSearchParams } from "react-router-dom";
import { apiUrls } from "../../const/api-urls";
import { useMessageBox } from "../message-box/message-box";
import Icon from "../icon/icon";
import PanelSplitter from "../panel-splitter/panel-splitter";
import { useResizablePanel } from "../../utils/use-resizable-panel";
import { MetadataEntity, invalidateQueryMetadata } from "../query-builder/query-design/query-builder-slice";
import EntityList from "./entity-definition/entity-list/entity-list";
import EntityBuilder from "./entity-definition/entity-builder/entity-builder";
import EntityDiagram from "./entity-diagram/entity-diagram";
import { EditableEntity, emptyEntity, toEditable } from "./types";
import FunctionDefinitionsWorkspace from "../function-definition/function-definition";
import OperatorDefinition from "./operator-definition/operator-definition";
import { resolveDefinitionChanges, useDefinitionUnsavedWarning } from "./definition-actions/definition-actions";
import type { DefinitionActionAdapter } from "./definition-actions/definition-actions";
import "./metadata-builder.css";

export * from "./types";

type NavigationGuard = (continueNavigation: () => void, cancelNavigation?: () => void) => void;
type MetadataSection = "diagram" | "entities" | "functions" | "operators";

async function request(url: string, options?: RequestInit) {
  const response = await fetch(url, options);
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(detail || `Metadata request failed (${response.status}).`);
  }
  return response.status === 204 ? null : response.json();
}

type MetadataBuilderProps = {
  onNavigationGuardChange?: (guard: ((continueNavigation: () => void, cancelNavigation?: () => void) => void) | null) => void;
  entityOnly?: boolean;
  embedded?: boolean;
};

export default function MetadataBuilder({ onNavigationGuardChange, entityOnly = false, embedded = false }: MetadataBuilderProps) {
  const dispatch = useDispatch();
  const showMessageBox = useMessageBox();
  const [searchParams, setSearchParams] = useSearchParams();
  const [functionNavigationGuard, setFunctionNavigationGuard] = useState<NavigationGuard | null>(null);
  const [operatorNavigationGuard, setOperatorNavigationGuard] = useState<NavigationGuard | null>(null);
  const updateFunctionNavigationGuard = useCallback(
    (guard: NavigationGuard | null) => setFunctionNavigationGuard(() => guard),
    [],
  );
  const updateOperatorNavigationGuard = useCallback(
    (guard: NavigationGuard | null) => setOperatorNavigationGuard(() => guard),
    [],
  );
  const [entities, setEntities] = useState<MetadataEntity[]>([]);
  const [selectedName, setSelectedName] = useState("");
  const selectedNameRef = useRef(selectedName);
  const [draft, setDraft] = useState<EditableEntity | null>(null);
  const [filter, setFilter] = useState("");
  const panel = useResizablePanel("metadata-builder");
  const [status, setStatus] = useState("loading");
  const [message, setMessage] = useState("");
  const [dirty, setDirty] = useState(false);
  useEffect(() => { selectedNameRef.current = selectedName; }, [selectedName]);
  const requestedSection = searchParams.get("section");
  const activeSection: MetadataSection = entityOnly ? "entities" : requestedSection === "diagram" || requestedSection === "entities" || requestedSection === "functions" || requestedSection === "operators"
    ? requestedSection
    : "diagram";
  useEffect(() => {
    if (entityOnly || requestedSection === "diagram" || requestedSection === "entities" || requestedSection === "functions" || requestedSection === "operators") return;
    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.set("section", "diagram");
    setSearchParams(nextSearchParams, { replace: true });
  }, [entityOnly, requestedSection, searchParams, setSearchParams]);
  const selectSection = async (section: MetadataSection) => {
    if (section === activeSection || !(await resolveAllDirtyChanges(activeSection))) return;
    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.set("section", section);
    setSearchParams(nextSearchParams);
  };
  const selectedIsBuiltin = entities.find((entity) => entity.name === selectedName)?.canDelete === false;
  const fieldCount = entities.reduce((count, entity) => count + entity.fields.length, 0);
  const relationCount = entities.reduce((count, entity) => count + (entity.relations || []).length, 0);
  useDefinitionUnsavedWarning(dirty);

  const loadEntities = async (selectFirst = false, preferredName = selectedName) => {
    setStatus("loading");
    setMessage("");
    try {
      const payload = await request(apiUrls.metadata.list);
      if (!Array.isArray(payload)) throw new Error("Metadata API must return an array of entities.");
      setEntities(payload);
      const preferred = payload.find((entity) => entity.name === preferredName);
      if (selectFirst || !preferredName || !preferred) {
        const next = selectFirst ? payload[0] : preferred;
        setSelectedName(next?.name || "");
        setDraft(next ? toEditable(next) : null);
      } else {
        setSelectedName(preferred.name);
        setDraft(toEditable(preferred));
      }
      setDirty(false);
      setStatus("succeeded");
      return true;
    } catch (error) {
      setStatus("failed");
      setMessage(error instanceof Error ? error.message : "Unable to load metadata.");
      return false;
    }
  };

  useEffect(() => { loadEntities(true); }, []);

  const discardChanges = () => {
    const original = entities.find((entity) => entity.name === selectedName);
    setDraft(original ? toEditable(original) : null);
    setDirty(false);
    setMessage("");
  };

  const save = async (): Promise<boolean> => {
    if (selectedIsBuiltin) {
      setMessage("Built-in metadata is read-only.");
      return false;
    }
    if (!draft?.name.trim() || draft.fields.some((field) => !field.name.trim())) {
      setMessage("Entity and field names are required.");
      return false;
    }
    if (draft.fields.some((field) => field.isRawSql && !field.dbColumn?.trim())) {
      setMessage("Raw SQL expressions are required.");
      return false;
    }
    const payload = {
      name: draft.name.trim(),
      schema: draft.schema.trim(),
      rawSql: draft.rawSql.trim() || draft.name.trim(),
      fields: draft.fields.map((field) => ({
        name: field.name.trim(),
        dbColumn: field.dbColumn?.trim() || field.name.trim(),
        dataType: field.dataType || "String",
        isRawSql: field.isRawSql ?? false,
      })),
      relations: draft.relations.map(({ name, relatedEntity, type, relatedKeys }) => ({ name: name.trim(), relatedEntity: relatedEntity.trim(), type, relatedKeys })),
    };
    setStatus("saving");
    setMessage("");
    try {
      const url = selectedName ? apiUrls.metadata.byName(selectedName) : apiUrls.metadata.list;
      await request(url, {
        method: selectedName ? "PUT" : "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      dispatch(invalidateQueryMetadata());
      return await loadEntities(false, payload.name);
    } catch (error) {
      setStatus("failed");
      setMessage(error instanceof Error ? error.message : "Unable to save metadata.");
      return false;
    }
  };

  const resolveEntityDirtyChanges = async () => {
    return resolveDefinitionChanges({
      resourceName: "entity",
      selectedName,
      dirty,
      saving: status === "saving",
      onSave: save,
      onDiscard: discardChanges,
    }, showMessageBox);
  };

  const resolveAllDirtyChanges = async () => {
    if (!(await resolveEntityDirtyChanges())) return false;
    if (functionNavigationGuard && !(await runNavigationGuard(functionNavigationGuard))) return false;
    if (operatorNavigationGuard && !(await runNavigationGuard(operatorNavigationGuard))) return false;
    return true;
  };

  const runNavigationGuard = (guard: NavigationGuard) => new Promise<boolean>((resolve) => {
    guard(() => resolve(true), () => resolve(false));
  });

  useEffect(() => {
    const guard = (continueNavigation: () => void, cancelNavigation?: () => void) => {
      void (async () => {
        if (await resolveAllDirtyChanges()) continueNavigation();
        else cancelNavigation?.();
      })();
    };
    onNavigationGuardChange?.(guard);
    return () => onNavigationGuardChange?.(null);
  }, [onNavigationGuardChange, dirty, draft, entities, selectedName, functionNavigationGuard, operatorNavigationGuard, activeSection]);

  const selectEntity = async (entity: MetadataEntity) => {
    if (entity.name === selectedName || !(await resolveEntityDirtyChanges())) return;
    setSelectedName(entity.name);
    setDraft(toEditable(entity));
    setDirty(false);
    setMessage("");
  };

  const updateDraft = (update: (entity: EditableEntity) => EditableEntity) => {
    if (!draft) return;
    setDraft(update(draft));
    setDirty(true);
  };

  const remove = async () => {
    const name = selectedNameRef.current;
    if (!name || selectedIsBuiltin) return;
    setStatus("saving");
    try {
      await request(apiUrls.metadata.byName(name), { method: "DELETE" });
      setSelectedName("");
      dispatch(invalidateQueryMetadata());
      await loadEntities(true);
    } catch (error) {
      setStatus("failed");
      setMessage(error instanceof Error ? error.message : "Unable to delete metadata.");
    }
  };

  const addEntity = async () => {
    setSelectedName("");
    setDraft(emptyEntity());
    setDirty(true);
    setMessage("");
  };

  const openTableAsEntity = (table: { name: string; schema: string; columns: { name: string; dataType: string; isPrimaryKey: boolean }[] }) => {
    const existing = entities.find((entity) => entity.name === table.name);
    if (existing) {
      setSelectedName(existing.name);
      setDraft(toEditable(existing));
    } else {
      setSelectedName("");
      setDraft({
        name: table.name,
        schema: table.schema,
        rawSql: table.name,
        fields: table.columns.map((column) => ({
          id: `diagram_${column.name}`,
          name: column.name,
          dbColumn: column.name,
          dataType: column.dataType,
          isRawSql: false,
        })),
        relations: [],
      });
    }
    setDirty(false);
    setMessage("");
    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.set("section", "entities");
    setSearchParams(nextSearchParams);
  };

  const entityActionAdapter: DefinitionActionAdapter = {
    resourceName: "entity",
    selectedName,
    hasSelection: Boolean(selectedName),
    getSelectedName: () => selectedNameRef.current,
    isBuiltIn: selectedIsBuiltin,
    hasDraft: Boolean(draft),
    dirty,
    saving: status === "saving",
    onSave: save,
    onDiscard: discardChanges,
    onDelete: remove,
    onNew: () => void addEntity(),
  };

  return (
    <>
      {!embedded && <section className="page-heading metadata-page-heading">
        <div className="metadata-heading-main">
          <span className="metadata-heading-mark" aria-hidden="true"><Icon name="schema" size={21} /></span>
          <div className="metadata-heading-copy">
            <span className="metadata-heading-eyebrow">DATA MODEL <i /> ENTITIES · FIELDS · RELATIONSHIPS</span>
            <h2>Define your data model</h2>
            <p>Describe your entities and relationships to make your data ready for querying.</p>
            <div className="metadata-heading-summary" aria-label="Metadata summary">
              <span className="metadata-heading-current"><Icon name="table" size={13} />{selectedName || "No entity selected"}</span>
              <span><strong>{status === "succeeded" ? entities.length : "—"}</strong> entities</span>
              <span><strong>{status === "succeeded" ? fieldCount : "—"}</strong> fields</span>
              <span><strong>{status === "succeeded" ? relationCount : "—"}</strong> relations</span>
            </div>
          </div>
        </div>
        {!entityOnly && <nav className="metadata-section-tabs" role="tablist" aria-label="Metadata definition types">
          <button type="button" role="tab" aria-selected={activeSection === "diagram"} className={activeSection === "diagram" ? "active" : ""} onClick={() => selectSection("diagram")}>
            <Icon name="schema" size={14} /> Diagram
          </button>
          <button type="button" role="tab" aria-selected={activeSection === "entities"} className={activeSection === "entities" ? "active" : ""} onClick={() => selectSection("entities")}>
            <Icon name="table" size={14} /> Entity definitions
          </button>
        </nav>}
      </section>}
      {!entityOnly && <div className="metadata-workspace-panel" hidden={activeSection !== "diagram"}>
        <EntityDiagram entities={entities} onOpenEntity={openTableAsEntity} />
      </div>}
      <div className="metadata-workspace-panel" hidden={activeSection !== "entities"}>
        <section className={`metadata-workspace split-workspace${panel.collapsed ? " panel-collapsed" : ""}`} style={panel.style}>
          <EntityList
            entities={entities}
            selectedName={selectedName}
            filter={filter}
            status={status}
            onFilterChange={setFilter}
            onSelectEntity={selectEntity}
          />
          <PanelSplitter panel={panel} label="entities panel" />
          <EntityBuilder
            draft={draft}
            selectedName={selectedName}
            canEdit={!selectedName || Boolean(draft?.canDelete)}
            message={message}
            allEntities={entities}
            actionAdapter={entityActionAdapter}
            onDraftChange={updateDraft}
          />
        </section>
      </div>
      {!entityOnly && <div className="metadata-workspace-panel" hidden={activeSection !== "functions"}>
        <FunctionDefinitionsWorkspace onNavigationGuardChange={updateFunctionNavigationGuard} />
      </div>}
      {!entityOnly && <div className="metadata-workspace-panel" hidden={activeSection !== "operators"}>
        <OperatorDefinition onNavigationGuardChange={updateOperatorNavigationGuard} />
      </div>}
    </>
  );
}
