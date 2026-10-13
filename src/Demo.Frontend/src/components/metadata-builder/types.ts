import { MetadataEntity, MetadataField, MetadataRelation } from "../query-builder/query-design/query-builder-slice";

export const dataTypes = ["String", "Number", "Datetime", "Boolean"];
export const relationTypes = ["Lookup", "Collection"];

let nextFieldId = 1;
export const createFieldId = () => `f_${nextFieldId++}`;

let nextRelationId = 1;
export const createRelationId = () => `r_${nextRelationId++}`;

export type RelationKey = { fromDbColumn: string; toDbColumn: string };
export type EditableField = MetadataField & { id?: string; fieldType: string; rawSql: string };
export type EditableRelation = MetadataRelation & { id?: string; type: string; relatedKeys: RelationKey[] };
export type EditableEntity = MetadataEntity & { rawSql: string; fields: EditableField[]; relations: EditableRelation[] };

export const emptyField = (): EditableField => ({ id: createFieldId(), name: "", dataType: "String", fieldType: "DbColumn", rawSql: "" });
export const emptyRelation = (): EditableRelation => ({ id: createRelationId(), name: "", relatedEntity: "", type: "Lookup", relatedKeys: [{ fromDbColumn: "", toDbColumn: "" }] });

export function toEditable(entity: MetadataEntity): EditableEntity {
  return {
    ...entity,
    rawSql: (entity as MetadataEntity & { rawSql?: string }).rawSql || entity.name,
    fields: entity.fields.map((field) => ({
      ...field,
      id: (field as Partial<EditableField>).id || createFieldId(),
      fieldType: (field as MetadataField & { fieldType?: string }).fieldType || "DbColumn",
      rawSql: (field as MetadataField & { rawSql?: string }).rawSql || field.name,
    })),
    relations: (entity.relations || []).map((relation) => ({
      ...relation,
      id: (relation as Partial<EditableRelation>).id || createRelationId(),
      type: String(relation.type ?? "Lookup"),
      relatedKeys: (relation as MetadataRelation & { relatedKeys?: RelationKey[] }).relatedKeys || [{ fromDbColumn: "", toDbColumn: "" }],
    })),
  };
}

export function emptyEntity(): EditableEntity {
  return { name: "", rawSql: "", fields: [emptyField()], relations: [] };
}
