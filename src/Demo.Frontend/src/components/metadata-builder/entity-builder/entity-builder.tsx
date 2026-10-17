import Icon from "../../icon/icon";
import { MetadataEntity } from "../../query-builder/query-design/query-builder-slice";
import { EditableEntity } from "../types";
import FieldsBuilder from "./fields-builder/fields-builder";
import RelationsBuilder from "./relations-builder/relations-builder";
import "./entity-builder.css";

type EntityBuilderProps = {
  draft: EditableEntity | null;
  selectedName: string;
  canEdit: boolean;
  dirty: boolean;
  status: string;
  message: string;
  allEntities: MetadataEntity[];
  onDraftChange: (updater: (draft: EditableEntity) => EditableEntity) => void;
  onSave: () => void;
  onRemove: () => void;
};

export default function EntityBuilder({
  draft,
  selectedName,
  canEdit,
  dirty,
  status,
  message,
  allEntities,
  onDraftChange,
  onSave,
  onRemove,
}: EntityBuilderProps) {
  if (!draft) {
    return (
      <main className="metadata-editor">
        <div className="metadata-empty">
          <Icon name="grid" size={32} />
          <p>Select an entity to view or edit its metadata.</p>
        </div>
      </main>
    );
  }

  return (
    <main className="metadata-editor">
      <div className="editor-head">
        <div>
          <span className="section-kicker">ENTITY DEFINITION</span>
          <h2>{selectedName ? canEdit ? "Edit entity" : "View entity" : "New entity"}</h2>
        </div>
        <div className="editor-actions">
          {selectedName && canEdit && (
            <button
              className="icon-button danger"
              type="button"
              title="Delete entity"
              onClick={onRemove}
            >
              <Icon name="trash" size={19} />
            </button>
          )}
          {canEdit
            ? <button
                className="button primary"
                type="button"
                onClick={onSave}
                disabled={status === "saving" || !dirty}
              >
                <Icon name="check" size={14} />{" "}
                {status === "saving" ? "Saving…" : "Save changes"}
              </button>
            : <span className="section-kicker">BUILT-IN · READ-ONLY</span>}
        </div>
      </div>

      {message && (
        <p className="metadata-alert" role="alert">
          {message}
        </p>
      )}

      <fieldset className="metadata-form" disabled={!canEdit}>
        <div className="metadata-form-grid">
          <label>
            Entity name
            <input
              value={draft.name}
              onChange={(event) =>
                onDraftChange((entity) => ({ ...entity, name: event.target.value }))
              }
              placeholder="e.g. Customers"
            />
          </label>
          <label>
            Database table / SQL
            <input
              value={draft.rawSql}
              onChange={(event) =>
                onDraftChange((entity) => ({ ...entity, rawSql: event.target.value }))
              }
              placeholder="Physical table name"
            />
          </label>
        </div>

        <FieldsBuilder
          fields={draft.fields}
          onChange={(fieldsUpdater) =>
            onDraftChange((entity) => ({
              ...entity,
              fields: fieldsUpdater(entity.fields),
            }))
          }
        />

        <RelationsBuilder
          relations={draft.relations}
          currentEntityName={draft.name}
          allEntities={allEntities}
          onChange={(relationsUpdater) =>
            onDraftChange((entity) => ({
              ...entity,
              relations: relationsUpdater(entity.relations),
            }))
          }
        />
      </fieldset>
    </main>
  );
}
