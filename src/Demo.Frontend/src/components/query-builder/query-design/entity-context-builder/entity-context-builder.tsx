import { useMemo, useState } from "react";
import type { EntityAliasDefinition, MetadataEntity, QueryDefinition, RelationKey } from "../query-builder-slice";
import { defaultEntityAlias, getRelationPathOptions } from "../../../../utils/query-entity-contexts";
import Icon from "../../../icon/icon";
import "./entity-context-builder.css";

type EntityContextBuilderProps = {
  metadata: MetadataEntity[];
  entity: MetadataEntity;
  definition: QueryDefinition;
  onDefinitionChange: (definition: QueryDefinition) => void;
};

function relationKeys(option: ReturnType<typeof getRelationPathOptions>[number] | undefined): RelationKey[] {
  return option?.relation.relatedKeys?.map((key) => ({ ...key })) ?? [{ fromDbColumn: "", toDbColumn: "" }];
}

export default function EntityContextBuilder({
  metadata,
  entity,
  definition,
  onDefinitionChange,
}: EntityContextBuilderProps) {
  const options = useMemo(() => getRelationPathOptions(entity, metadata), [entity, metadata]);
  const [relationPath, setRelationPath] = useState(options[0]?.path ?? "");
  const selectedRelationPath = options.some((option) => option.path === relationPath)
    ? relationPath
    : options[0]?.path ?? "";
  const selectedOption = options.find((option) => option.path === selectedRelationPath);
  const updateAliases = (entityAliases: EntityAliasDefinition[]) => onDefinitionChange({
    ...definition,
    entityAliases,
  });

  const addAlias = () => {
    if (!selectedOption) return;
    updateAliases([
      ...definition.entityAliases,
      {
        alias: defaultEntityAlias(selectedOption.path, [
          ...definition.entityAliases.map((alias) => alias.alias),
          ...options.map((option) => option.path),
        ]),
        relationPath: selectedOption.path,
        relatedKeys: relationKeys(selectedOption),
      },
    ]);
  };

  const updateAlias = (index: number, update: Partial<EntityAliasDefinition>) => {
    updateAliases(definition.entityAliases.map((alias, aliasIndex) =>
      aliasIndex === index ? { ...alias, ...update } : alias));
  };

  const changeRelationPath = (index: number, path: string) => {
    const option = options.find((candidate) => candidate.path === path);
    if (!option) return;
    updateAlias(index, {
      relationPath: path,
      relatedKeys: relationKeys(option),
    });
  };

  return (
    <section className="builder-section entity-context-section" aria-labelledby="entity-context-heading">
      <div className="section-header">
        <div className="section-heading">
          <span className="section-kicker" id="entity-context-heading">ENTITY CONTEXTS</span>
          <p className="filter-description">Add another named context from a relation path. Each context is independent and needs its own key mapping.</p>
        </div>
      </div>

      {definition.entityAliases.map((alias, index) => {
        const option = options.find((candidate) => candidate.path === alias.relationPath);
        const parentFields = option?.parentEntity.fields ?? [];
        const targetFields = option?.targetEntity.fields ?? [];

        return (
          <div className="entity-context-card" key={`${index}-${alias.relationPath}`}>
            <div className="entity-context-main">
              <label>
                <span>Context name</span>
                <input
                  aria-label={`Context ${index + 1} name`}
                  value={alias.alias}
                  onChange={(event) => updateAlias(index, { alias: event.target.value })}
                  placeholder="e.g. Invoice.Customer1"
                />
              </label>
              <label>
                <span>Based on</span>
                <select
                  aria-label={`Context ${index + 1} relation path`}
                  value={alias.relationPath}
                  onChange={(event) => changeRelationPath(index, event.target.value)}
                >
                  {options.map((candidate) => (
                    <option value={candidate.path} key={candidate.path}>{candidate.path}</option>
                  ))}
                </select>
              </label>
              <button
                type="button"
                className="entity-context-remove"
                aria-label={`Remove context ${alias.alias || index + 1}`}
                title="Remove context"
                onClick={() => updateAliases(definition.entityAliases.filter((_, aliasIndex) => aliasIndex !== index))}
              >
                <Icon name="close" size={14} />
              </button>
            </div>
            <div className="entity-context-key-list">
              <span className="entity-context-key-heading">Key mapping</span>
              {alias.relatedKeys.map((key, keyIndex) => (
                <div className="entity-context-key-row" key={keyIndex}>
                  <select
                    aria-label={`Context ${index + 1} source key ${keyIndex + 1}`}
                    value={key.fromDbColumn}
                    onChange={(event) => {
                      const relatedKeys = alias.relatedKeys.map((item, itemIndex) =>
                        itemIndex === keyIndex ? { ...item, fromDbColumn: event.target.value } : item);
                      updateAlias(index, { relatedKeys });
                    }}
                  >
                    <option value="">Select source field</option>
                    {parentFields.map((field) => <option value={field.name} key={field.name}>{field.name}</option>)}
                  </select>
                  <span aria-hidden="true">→</span>
                  <select
                    aria-label={`Context ${index + 1} target key ${keyIndex + 1}`}
                    value={key.toDbColumn}
                    onChange={(event) => {
                      const relatedKeys = alias.relatedKeys.map((item, itemIndex) =>
                        itemIndex === keyIndex ? { ...item, toDbColumn: event.target.value } : item);
                      updateAlias(index, { relatedKeys });
                    }}
                  >
                    <option value="">Select target field</option>
                    {targetFields.map((field) => <option value={field.name} key={field.name}>{field.name}</option>)}
                  </select>
                  <button
                    type="button"
                    className="entity-context-key-remove"
                    aria-label={`Remove key mapping ${keyIndex + 1} from context ${index + 1}`}
                    onClick={() => updateAlias(index, {
                      relatedKeys: alias.relatedKeys.filter((_, itemIndex) => itemIndex !== keyIndex),
                    })}
                    disabled={alias.relatedKeys.length === 1}
                  >
                    <Icon name="close" size={12} />
                  </button>
                </div>
              ))}
              <button
                type="button"
                className="text-button entity-context-add-key"
                onClick={() => updateAlias(index, {
                  relatedKeys: [...alias.relatedKeys, { fromDbColumn: "", toDbColumn: "" }],
                })}
              >
                Add key pair
              </button>
            </div>
          </div>
        );
      })}

      {options.length === 0
        ? <p className="empty-builder">This entity has no metadata relation paths to add.</p>
        : (
          <div className="entity-context-add-row">
            <label>
              <span>Relation path</span>
              <select value={selectedRelationPath} onChange={(event) => setRelationPath(event.target.value)}>
                {options.map((option) => <option value={option.path} key={option.path}>{option.path}</option>)}
              </select>
            </label>
            <button type="button" className="text-button" onClick={addAlias} disabled={!selectedOption}>
              <Icon name="plus" size={14} />
              Add entity context
            </button>
          </div>
        )}
    </section>
  );
}
