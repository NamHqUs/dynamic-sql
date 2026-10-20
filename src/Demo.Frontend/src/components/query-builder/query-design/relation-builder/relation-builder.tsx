import { useMemo, useState } from "react";
import type { RelationDefinition, MetadataEntity, QueryDefinition, RelationKey } from "../query-builder-slice";
import { defaultRelationName, getRelationPathOptions } from "../../../../utils/query-relations";
import Icon from "../../../icon/icon";
import "./relation-builder.css";

type RelationBuilderProps = {
  metadata: MetadataEntity[];
  entity: MetadataEntity;
  definition: QueryDefinition;
  onDefinitionChange: (definition: QueryDefinition) => void;
};

function relationKeys(option: ReturnType<typeof getRelationPathOptions>[number] | undefined): RelationKey[] {
  return option?.relation.relatedKeys?.map((key) => ({ ...key })) ?? [{ fromDbColumn: "", toDbColumn: "" }];
}

export default function RelationBuilder({
  metadata,
  entity,
  definition,
  onDefinitionChange,
}: RelationBuilderProps) {
  const options = useMemo(() => getRelationPathOptions(entity, metadata), [entity, metadata]);
  const [relationPath, setRelationPath] = useState(options[0]?.path ?? "");
  const selectedRelationPath = options.some((option) => option.path === relationPath)
    ? relationPath
    : options[0]?.path ?? "";
  const selectedOption = options.find((option) => option.path === selectedRelationPath);
  const updateRelations = (relations: RelationDefinition[]) => onDefinitionChange({
    ...definition,
    relations,
  });

  const addRelation = () => {
    if (!selectedOption) return;
    updateRelations([
      ...definition.relations,
      {
        name: defaultRelationName(selectedOption.path, [
          ...definition.relations.map((relation) => relation.name),
          ...options.map((option) => option.path),
        ]),
        relationPath: selectedOption.path,
        relatedKeys: relationKeys(selectedOption),
      },
    ]);
  };

  const updateRelation = (index: number, update: Partial<RelationDefinition>) => {
    updateRelations(definition.relations.map((relation, relationIndex) =>
      relationIndex === index ? { ...relation, ...update } : relation));
  };

  const changeRelationPath = (index: number, path: string) => {
    const option = options.find((candidate) => candidate.path === path);
    if (!option) return;
    updateRelation(index, {
      relationPath: path,
      relatedKeys: relationKeys(option),
    });
  };

  return (
    <section className="builder-section relation-section" aria-labelledby="relation-heading">
      <div className="section-header">
        <div className="section-heading">
          <span className="section-kicker" id="relation-heading">RELATIONS</span>
          <p className="filter-description">Add another named relation from a relation path. Each relation is independent and needs its own key mapping.</p>
        </div>
      </div>

      {definition.relations.map((relation, index) => {
        const option = options.find((candidate) => candidate.path === relation.relationPath);
        const parentFields = option?.parentEntity.fields ?? [];
        const targetFields = option?.targetEntity.fields ?? [];

        return (
          <div className="relation-card" key={`${index}-${relation.relationPath}`}>
            <div className="relation-main">
              <label>
                <span>Relation name</span>
                <input
                  aria-label={`Relation ${index + 1} name`}
                  value={relation.name}
                  onChange={(event) => updateRelation(index, { name: event.target.value })}
                  placeholder="e.g. Invoice.Customer1"
                />
              </label>
              <label>
                <span>Based on</span>
                <select
                  aria-label={`Relation ${index + 1} relation path`}
                  value={relation.relationPath}
                  onChange={(event) => changeRelationPath(index, event.target.value)}
                >
                  {options.map((candidate) => (
                    <option value={candidate.path} key={candidate.path}>{candidate.path}</option>
                  ))}
                </select>
              </label>
              <button
                type="button"
                className="relation-remove"
                aria-label={`Remove relation ${relation.name || index + 1}`}
                title="Remove relation"
                onClick={() => updateRelations(definition.relations.filter((_, relationIndex) => relationIndex !== index))}
              >
                <Icon name="close" size={14} />
              </button>
            </div>
            <div className="relation-key-list">
              <span className="relation-key-heading">Key mapping</span>
              {relation.relatedKeys.map((key, keyIndex) => (
                <div className="relation-key-row" key={keyIndex}>
                  <select
                    aria-label={`Relation ${index + 1} source key ${keyIndex + 1}`}
                    value={key.fromDbColumn}
                    onChange={(event) => {
                      const relatedKeys = relation.relatedKeys.map((item, itemIndex) =>
                        itemIndex === keyIndex ? { ...item, fromDbColumn: event.target.value } : item);
                      updateRelation(index, { relatedKeys });
                    }}
                  >
                    <option value="">Select source field</option>
                    {parentFields.map((field) => <option value={field.name} key={field.name}>{field.name}</option>)}
                  </select>
                  <span aria-hidden="true">→</span>
                  <select
                    aria-label={`Relation ${index + 1} target key ${keyIndex + 1}`}
                    value={key.toDbColumn}
                    onChange={(event) => {
                      const relatedKeys = relation.relatedKeys.map((item, itemIndex) =>
                        itemIndex === keyIndex ? { ...item, toDbColumn: event.target.value } : item);
                      updateRelation(index, { relatedKeys });
                    }}
                  >
                    <option value="">Select target field</option>
                    {targetFields.map((field) => <option value={field.name} key={field.name}>{field.name}</option>)}
                  </select>
                  <button
                    type="button"
                    className="relation-key-remove"
                    aria-label={`Remove key mapping ${keyIndex + 1} from relation ${index + 1}`}
                    onClick={() => updateRelation(index, {
                      relatedKeys: relation.relatedKeys.filter((_, itemIndex) => itemIndex !== keyIndex),
                    })}
                    disabled={relation.relatedKeys.length === 1}
                  >
                    <Icon name="close" size={12} />
                  </button>
                </div>
              ))}
              <button
                type="button"
                className="text-button relation-add-key"
                onClick={() => updateRelation(index, {
                  relatedKeys: [...relation.relatedKeys, { fromDbColumn: "", toDbColumn: "" }],
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
          <div className="relation-add-row">
            <label>
              <span>Relation path</span>
              <select value={selectedRelationPath} onChange={(event) => setRelationPath(event.target.value)}>
                {options.map((option) => <option value={option.path} key={option.path}>{option.path}</option>)}
              </select>
            </label>
            <button type="button" className="text-button" onClick={addRelation} disabled={!selectedOption}>
              <Icon name="plus" size={14} />
              Add relation
            </button>
          </div>
        )}
    </section>
  );
}
