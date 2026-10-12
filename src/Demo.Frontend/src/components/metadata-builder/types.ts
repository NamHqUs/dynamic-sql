import { MetadataEntity, MetadataField, MetadataRelation } from "../query-builder/query-design/query-builder-slice";

export const dataTypes = ["String", "Number", "Datetime", "Boolean"];
export const relationTypes = ["Lookup", "Collection"];

export type RelationKey = { fromDbColumn: string; toDbColumn: string };
export type EditableRelation = MetadataRelation & { type: string; relatedKeys: RelationKey[] };
export type EditableEntity = MetadataEntity & { rawSql: string; fields: (MetadataField & { fieldType: string; rawSql: string })[]; relations: EditableRelation[] };

export const emptyField = (): EditableEntity["fields"][number] => ({ name: "", dataType: "String", fieldType: "DbColumn", rawSql: "" });
export const emptyRelation = (): EditableRelation => ({ name: "", relatedEntity: "", type: "Lookup", relatedKeys: [{ fromDbColumn: "", toDbColumn: "" }] });

export function toEditable(entity: MetadataEntity): EditableEntity {
  return {
    ...entity,
    rawSql: (entity as MetadataEntity & { rawSql?: string }).rawSql || entity.name,
    fields: entity.fields.map((field) => ({
      ...field,
      fieldType: (field as MetadataField & { fieldType?: string }).fieldType || "DbColumn",
      rawSql: (field as MetadataField & { rawSql?: string }).rawSql || field.name,
    })),
    relations: (entity.relations || []).map((relation) => ({
      ...relation,
      type: String(relation.type ?? "Lookup"),
      relatedKeys: (relation as MetadataRelation & { relatedKeys?: RelationKey[] }).relatedKeys || [{ fromDbColumn: "", toDbColumn: "" }],
    })),
  };
}

export function emptyEntity(): EditableEntity {
  return { name: "", rawSql: "", fields: [emptyField()], relations: [] };
}
