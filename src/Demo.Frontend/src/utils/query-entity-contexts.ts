import type { EntityAliasDefinition, MetadataEntity, MetadataRelation } from "../components/query-builder/query-design/query-builder-slice";

export type RelationPathOption = {
  path: string;
  parentPath: string;
  parentEntity: MetadataEntity;
  targetEntity: MetadataEntity;
  relation: MetadataRelation;
};

export function getRelationPathOptions(root: MetadataEntity, metadata: MetadataEntity[]): RelationPathOption[] {
  const options: RelationPathOption[] = [];
  const visit = (entity: MetadataEntity, prefix: string, ancestors: Set<string>, depth: number) => {
    if (depth >= 5) return;
    for (const relation of entity.relations ?? []) {
      const target = metadata.find((item) => item.name === relation.relatedEntity);
      if (!target) continue;
      const path = prefix ? `${prefix}.${relation.name}` : relation.name;
      options.push({
        path,
        parentPath: prefix,
        parentEntity: entity,
        targetEntity: target,
        relation,
      });
      if (!ancestors.has(target.name)) {
        visit(target, path, new Set(ancestors).add(target.name), depth + 1);
      }
    }
  };

  visit(root, "", new Set([root.name]), 0);
  return options;
}

function resolveEntityPath(root: MetadataEntity, metadata: MetadataEntity[], path: string): MetadataEntity | undefined {
  let current: MetadataEntity | undefined = root;
  for (const segment of path.split(".").filter(Boolean)) {
    const relation = current?.relations?.find((item) => item.name === segment);
    current = relation && metadata.find((item) => item.name === relation.relatedEntity);
    if (!current) return undefined;
  }
  return current;
}

export function addEntityAliasesToMetadata(
  metadata: MetadataEntity[],
  rootName: string,
  aliases: EntityAliasDefinition[],
): MetadataEntity[] {
  const result = metadata.map((entity) => ({
    ...entity,
    fields: [...entity.fields],
    relations: [...(entity.relations ?? [])],
  }));
  const root = result.find((entity) => entity.name === rootName);
  if (!root) return result;

  for (const alias of aliases) {
    const path = alias.alias.split(".");
    const relationName = path.pop();
    if (!relationName) continue;
    const parent = resolveEntityPath(root, result, path.join("."));
    const target = resolveEntityPath(root, result, alias.relationPath);
    if (!parent || !target) continue;
    parent.relations = [
      ...(parent.relations ?? []).filter((relation) =>
        relation.name.toLowerCase() !== relationName.toLowerCase()),
      {
        name: relationName,
        relatedEntity: target.name,
        type: "Lookup",
        relatedKeys: alias.relatedKeys,
      },
    ];
  }

  return result;
}

export function defaultEntityAlias(path: string, existingAliases: string[]): string {
  const segments = path.split(".");
  const relationName = segments.pop() ?? "Entity";
  const parentPath = segments.join(".");
  let suffix = 1;
  let alias = "";
  do {
    const name = `${relationName}${suffix}`;
    alias = parentPath ? `${parentPath}.${name}` : name;
    suffix++;
  } while (existingAliases.some((existing) => existing.toLowerCase() === alias.toLowerCase()));
  return alias;
}
