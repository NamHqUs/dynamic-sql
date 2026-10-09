import { useEffect } from "react";
import { useDispatch, useSelector } from "react-redux";
import DataEditor from "../data-editor/data-editor";
import Icon from "../../icon/icon";
import { parseCsv, toCsv } from "../../../utils/csv";
import {
  addEntity,
  clearEntityPreview,
  deleteEntity,
  preservePrimaryEntityName,
  setEntityPreview,
  setError,
  setSearchTerm,
  syncPrimaryEntity,
  toggleEntityCollapsed,
  updateEntity,
} from "./raw-data-slice";
import "./raw-data.css";

export default function RawData({ rows, sourceName, onApply }) {
  const dispatch = useDispatch();
  const { entities, previewEntityIds, collapsedEntityIds, searchTerm, error } = useSelector((state) => state.rawData);

  useEffect(() => {
    dispatch(syncPrimaryEntity({ rows, sourceName, text: toCsv(rows) }));
  }, [dispatch, rows, sourceName]);

  const toggleEntity = (entityId) => {
    dispatch(toggleEntityCollapsed(entityId));
  };

  const normalizedSearchTerm = searchTerm.trim().toLowerCase();
  const filteredEntities = normalizedSearchTerm
    ? entities.filter((entity) => entity.name.toLowerCase().includes(normalizedSearchTerm))
    : entities;

  const applyEntity = (id) => {
    const entity = entities.find((item) => item.id === id);
    const parsed = parseCsv(entity.text);
    if (!parsed.length) {
      dispatch(setError("Enter a header row and at least one data row (comma-separated)."));
      return;
    }

    dispatch(updateEntity({ id, changes: { rows: parsed } }));
    dispatch(setEntityPreview(id));
    dispatch(preservePrimaryEntityName(id !== "customer_revenue"));
    onApply(parsed, entity.name);
  };

  return (
    <div className="source-panel raw-data">
      <div className="entity-editor">
        <div className="entity-editor-heading">
          <div>
            <span className="section-kicker">RAW DATA</span>
            <strong>{entities.length} {entities.length === 1 ? "entity" : "entities"} in this source</strong>
          </div>
          <div className="entity-search">
            <Icon name="search" size={14} />
            <input value={searchTerm} onChange={(event) => dispatch(setSearchTerm(event.target.value))} placeholder="Search entities" aria-label="Search entities" />
            {searchTerm && <button className="entity-search-clear" onClick={() => dispatch(setSearchTerm(""))} aria-label="Clear entity search" title="Clear search"><Icon name="close" size={13} /></button>}
          </div>
        </div>
        {filteredEntities.map((entity) => (
          <div className={`entity-editor-card${collapsedEntityIds.includes(entity.id) ? " is-collapsed" : ""}`} key={entity.id}>
            <div className="entity-title-row">
              <input className="entity-name" value={entity.name} onChange={(event) => dispatch(updateEntity({ id: entity.id, changes: { name: event.target.value } }))} readOnly={previewEntityIds.includes(entity.id)} aria-label={`${entity.name || "Entity"} name`} />
              <span className="entity-state"><span className="meta-dot green" /> Entity</span>
              {previewEntityIds.includes(entity.id)
                ? <button className="button secondary entity-apply" onClick={() => dispatch(clearEntityPreview(entity.id))}><Icon name="edit" size={14} /> Edit Data</button>
                : <button className="button primary entity-apply" onClick={() => applyEntity(entity.id)}><Icon name="check" size={14} /> Apply Data</button>}
              <button className="icon-button entity-collapse" onClick={() => toggleEntity(entity.id)} title={`${collapsedEntityIds.includes(entity.id) ? "Expand" : "Collapse"} ${entity.name}`} aria-label={`${collapsedEntityIds.includes(entity.id) ? "Expand" : "Collapse"} ${entity.name}`} aria-expanded={!collapsedEntityIds.includes(entity.id)}><Icon name="chevron" size={15} /></button>
              <button className="icon-button entity-delete" onClick={() => dispatch(deleteEntity(entity.id))} title={`Delete ${entity.name}`} aria-label={`Delete ${entity.name}`}><Icon name="trash" size={15} /></button>
            </div>
            {!collapsedEntityIds.includes(entity.id) && <>
              <div className="source-copy">
                <p>Give this entity a name, then paste comma-separated rows below. Each entity can be queried independently.</p>
              </div>
              <div className="source-editor">
                <DataEditor text={entity.text} error={error} rows={entity.rows} showPreview={previewEntityIds.includes(entity.id)} onChange={(value) => dispatch(updateEntity({ id: entity.id, changes: { text: value } }))} onApply={() => applyEntity(entity.id)} onEdit={() => dispatch(clearEntityPreview(entity.id))} showApplyButton={false} showEditButton={false} />
              </div>
            </>}
          </div>
        ))}
        {!entities.length && <div className="empty-state entity-selection-empty">Add an entity to start defining raw data.</div>}
        {entities.length > 0 && !filteredEntities.length && <div className="empty-state entity-selection-empty">No entities match: {searchTerm}</div>}
        <div className="entity-add-bottom">
          <button className="button secondary" onClick={() => dispatch(addEntity())}><Icon name="plus" size={14} /> Add entity</button>
        </div>
      </div>
    </div>
  );
}
