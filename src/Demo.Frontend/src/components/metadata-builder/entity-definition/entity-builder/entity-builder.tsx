import Icon from "../../../icon/icon";
import { MetadataEntity } from "../../../query-builder/query-design/query-builder-slice";
import { EditableEntity } from "../../types";
import FieldsBuilder from "./fields-builder/fields-builder";
import RelationsBuilder from "./relations-builder/relations-builder";
import "./entity-builder.css";

type EntityBuilderProps = {
  draft: EditableEntity | null;
  canEdit: boolean;
  message: string;
  allEntities: MetadataEntity[];
  onDraftChange: (updater: (draft: EditableEntity) => EditableEntity) => void;
  compact?: boolean;
};

export default function EntityBuilder({
  draft,
  canEdit,
  message,
  allEntities,
  onDraftChange,
  compact = false,
}: EntityBuilderProps) {
  return (
    <div className={`metadata-editor-content${compact ? " metadata-editor-compact" : ""}`}>
      {!draft
        ? <div className="metadata-empty">
            <Icon name="table" size={32} />
            <p>Select an entity to view or edit its metadata.</p>
          </div>
        : <>
          {message && (
            <p className="metadata-alert" role="alert">
              {message}
            </p>
          )}
          <div className="metadata-form">
            <fieldset className="metadata-form-fieldset" disabled={!canEdit}>
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
            SQL Statement
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
          </div>
        </>}
    </div>
  );
}
