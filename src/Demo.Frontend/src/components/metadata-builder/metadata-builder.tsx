import { useEffect, useState } from "react";
import { useDispatch } from "react-redux";
import { apiUrls } from "../../const/api-urls";
import { useMessageBox } from "../message-box/message-box";
import Icon from "../icon/icon";
import PanelSplitter from "../panel-splitter/panel-splitter";
import { useResizablePanel } from "../../utils/use-resizable-panel";
import { MetadataEntity, clearQueryResult, invalidateQueryMetadata } from "../query-builder/query-design/query-builder-slice";
import { resetQueryBuilderCache } from "../query-builder/query-builder";
import EntityList from "./entity-list/entity-list";
import EntityBuilder from "./entity-builder/entity-builder";
import { EditableEntity, emptyEntity, toEditable } from "./types";
import "./metadata-builder.css";

export * from "./types";

async function request(url: string, options?: RequestInit) {
  const response = await fetch(url, options);
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(detail || `Metadata request failed (${response.status}).`);
  }
  return response.status === 204 ? null : response.json();
}

type MetadataBuilderProps = {
  onNavigationGuardChange?: (guard: ((continueNavigation: () => void) => void) | null) => void;
};

export default function MetadataBuilder({ onNavigationGuardChange }: MetadataBuilderProps) {
  const dispatch = useDispatch();
  const showMessageBox = useMessageBox();
  const [entities, setEntities] = useState<MetadataEntity[]>([]);
  const [selectedName, setSelectedName] = useState("");
  const [draft, setDraft] = useState<EditableEntity | null>(null);
  const [filter, setFilter] = useState("");
  const panel = useResizablePanel("metadata-builder");
  const [status, setStatus] = useState("loading");
  const [message, setMessage] = useState("");
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!dirty) return;
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [dirty]);

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
    if (!draft?.name.trim() || draft.fields.some((field) => !field.name.trim())) {
      setMessage("Entity and field names are required.");
      return false;
    }
    const payload = {
      name: draft.name.trim(),
      rawSql: draft.rawSql.trim() || draft.name.trim(),
      fields: draft.fields.map(({ name, dataType, fieldType, rawSql }) => ({ name: name.trim(), dataType, fieldType, rawSql: rawSql.trim() || name.trim() })),
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

  const resolveDirtyChanges = async () => {
    if (!dirty) return true;
    const action = await showMessageBox({
      eyebrow: "UNSAVED METADATA",
      title: "Apply your metadata changes?",
      message: "Save your changes before continuing, discard them, or stay here to keep editing.",
      actions: [
        { value: "stay", label: "Stay here", variant: "secondary" },
        { value: "discard", label: "Discard changes", variant: "secondary" },
        { value: "apply", label: "Apply changes", variant: "primary" },
      ],
    });
    if (action === "apply") return save();
    if (action === "discard") {
      discardChanges();
      return true;
    }
    return false;
  };

  useEffect(() => {
    const guard = (continueNavigation: () => void) => {
      void (async () => {
        if (await resolveDirtyChanges()) continueNavigation();
      })();
    };
    onNavigationGuardChange?.(guard);
    return () => onNavigationGuardChange?.(null);
  }, [onNavigationGuardChange, dirty, draft, entities, selectedName]);

  const selectEntity = async (entity: MetadataEntity) => {
    if (!(await resolveDirtyChanges())) return;
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
    if (!selectedName || !(await resolveDirtyChanges())) return;
    const action = await showMessageBox({
      eyebrow: "DELETE ENTITY",
      title: `Delete ${selectedName}?`,
      message: "This entity definition will be permanently removed.",
      actions: [
        { value: "cancel", label: "Cancel", variant: "secondary" },
        { value: "delete", label: "Delete entity", variant: "danger" },
      ],
    });
    if (action !== "delete") return;
    setStatus("saving");
    try {
      await request(apiUrls.metadata.byName(selectedName), { method: "DELETE" });
      setSelectedName("");
      dispatch(invalidateQueryMetadata());
      await loadEntities(true);
    } catch (error) {
      setStatus("failed");
      setMessage(error instanceof Error ? error.message : "Unable to delete metadata.");
    }
  };

  const reset = async () => {
    const action = await showMessageBox({
      eyebrow: "RESET METADATA",
      title: "Restore original definitions?",
      message: "All current metadata changes will be lost and the original definitions will be restored.",
      actions: [
        { value: "cancel", label: "Cancel", variant: "secondary" },
        { value: "reset", label: "Reset metadata", variant: "danger" },
      ],
    });
    if (action !== "reset") return;
    setStatus("saving");
    setMessage("");
    try {
      await request(apiUrls.metadata.reset, { method: "POST" });
      resetQueryBuilderCache();
      dispatch(clearQueryResult());
      dispatch(invalidateQueryMetadata());
      await loadEntities(true);
    } catch (error) {
      setStatus("failed");
      setMessage(error instanceof Error ? error.message : "Unable to reset metadata.");
    }
  };

  const addEntity = async () => {
    if (!(await resolveDirtyChanges())) return;
    setSelectedName("");
    setDraft(emptyEntity());
    setDirty(true);
    setMessage("");
  };

  return (
    <>
      <section className="page-heading">
        <div>
          <div className="eyebrow">WORKSPACE / METADATA BUILDER</div>
          <h1>Design entity metadata</h1>
          <p>Shape the fields and relationships that power your dynamic queries.</p>
        </div>
        <div className="heading-actions">
          <button className="button secondary" onClick={reset}><Icon name="reset" size={14} /> Reset</button>
          <button className="button primary" onClick={addEntity}><Icon name="plus" size={14} /> New entity</button>
        </div>
      </section>
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
          dirty={dirty}
          status={status}
          message={message}
          allEntities={entities}
          onDraftChange={updateDraft}
          onSave={save}
          onRemove={remove}
        />
      </section>
    </>
  );
}
