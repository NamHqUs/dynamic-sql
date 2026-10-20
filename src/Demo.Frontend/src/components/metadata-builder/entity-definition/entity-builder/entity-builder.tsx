import Icon from "../../../icon/icon";
import { MetadataEntity } from "../../../query-builder/query-design/query-builder-slice";
import { EditableEntity } from "../../types";
import DefinitionActions from "../../definition-actions/definition-actions";
import type { DefinitionActionAdapter } from "../../definition-actions/definition-actions";
import FieldsBuilder from "./fields-builder/fields-builder";
import RelationsBuilder from "./relations-builder/relations-builder";
import "./entity-builder.css";

type EntityBuilderProps = {
  draft: EditableEntity | null;
  selectedName: string;
  canEdit: boolean;
  message: string;
  allEntities: MetadataEntity[];
  actionAdapter: DefinitionActionAdapter;
  onDraftChange: (updater: (draft: EditableEntity) => EditableEntity) => void;
};

export default function EntityBuilder({
  draft,
  selectedName,
  canEdit,
  message,
  allEntities,
  actionAdapter,
  onDraftChange,
}: EntityBuilderProps) {
  return (
    <main className="metadata-editor">
      <div className="editor-head">
        <div>
          <span className="section-kicker">ENTITY DEFINITION</span>
          <h2>{selectedName || (draft ? "New Entity…" : "Select an entity")}</h2>
        </div>
        <DefinitionActions adapter={actionAdapter} />
      </div>

      {!draft
        ? <div className="metadata-empty">
            <Icon name="grid" size={32} />
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
          </div>
        </>}
    </main>
  );
}
