import Icon from "../../icon/icon";
import { MetadataEntity } from "../../query-builder/query-design/query-builder-slice";
import "./entity-list.css";

type EntityListProps = {
  entities: MetadataEntity[];
  selectedName: string;
  filter: string;
  status: string;
  onFilterChange: (filter: string) => void;
  onSelectEntity: (entity: MetadataEntity) => void;
};

export default function EntityList({
  entities,
  selectedName,
  filter,
  status,
  onFilterChange,
  onSelectEntity,
}: EntityListProps) {
  const visibleEntities = entities.filter((entity) =>
    entity.name.toLowerCase().includes(filter.toLowerCase()),
  );

  return (
    <aside className="metadata-list split-panel">
      <div className="metadata-list-head">
        <span className="section-kicker">ENTITIES</span>
        <span className="tab-number">{entities.length}</span>
      </div>
      <label className="metadata-search">
        <Icon name="search" size={14} />
        <input
          value={filter}
          onChange={(event) => onFilterChange(event.target.value)}
          placeholder="Find an entity"
        />
      </label>
      {status === "loading" && <p className="metadata-message">Loading metadata…</p>}
      {status !== "loading" &&
        visibleEntities.map((entity) => (
          <button
            className={`metadata-entity${entity.name === selectedName ? " active" : ""}`}
            key={entity.name}
            onClick={() => onSelectEntity(entity)}
          >
            <span className="entity-icon">
              <Icon name="grid" size={14} />
            </span>
            <span>
              <b>{entity.name}</b>
              <small>
                {entity.fields.length} fields · {(entity.relations || []).length} relations
              </small>
            </span>
            <Icon name="chevron" size={14} />
          </button>
        ))}
      {status !== "loading" && !visibleEntities.length && (
        <p className="metadata-message">No matching entities.</p>
      )}
    </aside>
  );
}
