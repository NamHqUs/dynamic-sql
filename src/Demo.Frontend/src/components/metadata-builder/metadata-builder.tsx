import { useEffect, useState } from "react";
import { useDispatch } from "react-redux";
import { apiUrls } from "../../const/api-urls";
import Icon from "../icon/icon";
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

type MetadataBuilderProps = { onDirtyChange?: (dirty: boolean) => void };

export default function MetadataBuilder({ onDirtyChange }: MetadataBuilderProps) {
  const dispatch = useDispatch();
  const [entities, setEntities] = useState<MetadataEntity[]>([]);
  const [selectedName, setSelectedName] = useState("");
  const [draft, setDraft] = useState<EditableEntity | null>(null);
  const [filter, setFilter] = useState("");
  const [status, setStatus] = useState("loading");
  const [message, setMessage] = useState("");
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    onDirtyChange?.(dirty);
  }, [dirty, onDirtyChange]);

  useEffect(() => {
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!dirty) return;
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [dirty]);

  const confirmDiscardChanges = () => !dirty || window.confirm("You have unsaved changes. Leave without saving?");

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
    } catch (error) {
      setStatus("failed");
      setMessage(error instanceof Error ? error.message : "Unable to load metadata.");
    }
  };

  useEffect(() => { loadEntities(true); }, []);

  const selectEntity = (entity: MetadataEntity) => {
    if (!confirmDiscardChanges()) return;
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

  const save = async () => {
    if (!draft?.name.trim() || draft.fields.some((field) => !field.name.trim())) {
      setMessage("Entity and field names are required.");
      return;
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
      await loadEntities(false, payload.name);
    } catch (error) {
      setStatus("failed");
      setMessage(error instanceof Error ? error.message : "Unable to save metadata.");
    }
  };

  const remove = async () => {
    if (!selectedName || !confirmDiscardChanges() || !window.confirm(`Delete the ${selectedName} entity definition?`)) return;
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
    if (!window.confirm("Reset metadata to the original definitions? All changes will be lost.")) return;
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

  const addEntity = () => {
    if (!confirmDiscardChanges()) return;
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
      <section className="metadata-workspace">
        <EntityList
          entities={entities}
          selectedName={selectedName}
          filter={filter}
          status={status}
          onFilterChange={setFilter}
          onSelectEntity={selectEntity}
        />
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
