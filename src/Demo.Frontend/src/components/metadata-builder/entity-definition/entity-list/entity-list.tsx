import Icon from "../../../icon/icon";
import { MetadataEntity } from "../../../query-builder/query-design/query-builder-slice";
import MetadataSidebar, { type MetadataSidebarGroup } from "../../metadata-sidebar/metadata-sidebar";
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
  const customEntities = visibleEntities.filter((entity) => entity.canDelete !== false);
  const builtInEntities = visibleEntities.filter((entity) => entity.canDelete === false);
  const renderEntity = (entity: MetadataEntity) => (
    <button
      className={`metadata-entity${entity.name === selectedName ? " active" : ""}`}
      key={entity.name}
      onClick={() => onSelectEntity(entity)}
    >
      <span className="entity-icon"><Icon name="table" size={14} /></span>
      <span>
        <b>{entity.name}</b>
        <small>{entity.fields.length} fields · {(entity.relations || []).length} relations</small>
      </span>
      <Icon name="chevron" size={14} />
    </button>
  );
  const groups: MetadataSidebarGroup[] = [
    {
      id: "entity-custom-list",
      title: "Custom",
      count: customEntities.length,
      content: customEntities.map(renderEntity),
      emptyMessage: status !== "loading" && !customEntities.length ? "No custom entities." : undefined,
    },
    {
      id: "entity-builtin-list",
      title: "Builtin",
      count: builtInEntities.length,
      content: builtInEntities.map(renderEntity),
      emptyMessage: status !== "loading" && !builtInEntities.length ? "No built-in entities." : undefined,
    },
  ];

  return (
    <MetadataSidebar
      className="metadata-list split-panel"
      title="ENTITIES"
      totalCount={entities.length}
      filter={filter}
      searchPlaceholder="Find an entity"
      loadingMessage={status === "loading" ? "Loading metadata…" : undefined}
      groups={groups}
      onFilterChange={onFilterChange}
    />
  );
}
