import { useState } from "react";
import Icon from "../../../../icon/icon";
import { EditableEntity, dataTypes, emptyField } from "../../../types";
import "./fields-builder.css";

type FieldsBuilderProps = {
  fields: EditableEntity["fields"];
  onChange: (updater: (fields: EditableEntity["fields"]) => EditableEntity["fields"]) => void;
};

export default function FieldsBuilder({ fields, onChange }: FieldsBuilderProps) {
  const [draggedFieldIndex, setDraggedFieldIndex] = useState<number | null>(null);
  const [dropTargetFieldIndex, setDropTargetFieldIndex] = useState<number | null>(null);

  function moveField(fromIndex: number, toIndex: number) {
    if (fromIndex === toIndex || fromIndex < 0 || toIndex < 0 || fromIndex >= fields.length || toIndex >= fields.length) {
      return;
    }
    onChange((prevFields) => {
      const nextFields = [...prevFields];
      const [moved] = nextFields.splice(fromIndex, 1);
      nextFields.splice(toIndex, 0, moved);
      return nextFields;
    });
  }

  function updateField<Key extends keyof EditableEntity["fields"][number]>(
    index: number,
    key: Key,
    value: EditableEntity["fields"][number][Key],
  ) {
    onChange((prevFields) =>
      prevFields.map((field, fieldIndex) =>
        fieldIndex === index ? { ...field, [key]: value } : field,
      ),
    );
  }

  function addField() {
    onChange((prevFields) => [...prevFields, emptyField()]);
  }

  function removeField(index: number) {
    onChange((prevFields) => prevFields.filter((_, fieldIndex) => fieldIndex !== index));
  }

  return (
    <div className="editor-section">
      <div className="editor-section-head">
        <div>
          <h3>Fields</h3>
          <p>Columns exposed to the query builder.</p>
        </div>
      </div>
      <div className="field-editor-table">
        <div className="field-editor-head">
          <span className="drag-handle-col" />
          <span>Name</span>
          <span>Data type</span>
          <span>Field type</span>
          <span>Raw SQL (optional)</span>
          <span />
        </div>
        {fields.map((field, index) => (
          <div
            className={`field-editor-row${draggedFieldIndex === index ? " dragging" : ""}${dropTargetFieldIndex === index ? " drop-target" : ""}`}
            key={field.id ?? index}
            onDragOver={(event) => {
              if (draggedFieldIndex !== null && draggedFieldIndex !== index) {
                event.preventDefault();
                event.dataTransfer.dropEffect = "move";
                setDropTargetFieldIndex(index);
              }
            }}
            onDrop={(event) => {
              event.preventDefault();
              const rawIndex =
                event.dataTransfer.getData("application/x-field-index") ||
                event.dataTransfer.getData("text/plain");
              const sourceIndex = rawIndex !== "" ? Number(rawIndex) : draggedFieldIndex;
              if (sourceIndex !== null && !Number.isNaN(sourceIndex) && sourceIndex !== index) {
                moveField(sourceIndex, index);
              }
              setDraggedFieldIndex(null);
              setDropTargetFieldIndex(null);
            }}
          >
            <span
              className="field-row-drag-handle"
              draggable
              title="Drag to reorder"
              aria-label="Drag to reorder"
              onDragStart={(event) => {
                setDraggedFieldIndex(index);
                event.dataTransfer.effectAllowed = "move";
                event.dataTransfer.setData("text/plain", String(index));
                event.dataTransfer.setData("application/x-field-index", String(index));
              }}
              onDragEnd={() => {
                setDraggedFieldIndex(null);
                setDropTargetFieldIndex(null);
              }}
            >
              <svg
                className="icon"
                width={14}
                height={14}
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <circle cx="9" cy="5" r="1" fill="currentColor" />
                <circle cx="9" cy="12" r="1" fill="currentColor" />
                <circle cx="9" cy="19" r="1" fill="currentColor" />
                <circle cx="15" cy="5" r="1" fill="currentColor" />
                <circle cx="15" cy="12" r="1" fill="currentColor" />
                <circle cx="15" cy="19" r="1" fill="currentColor" />
              </svg>
            </span>
            <input
              value={field.name}
              onChange={(event) => updateField(index, "name", event.target.value)}
              placeholder="Field name"
            />
            <select
              value={field.dataType}
              onChange={(event) => updateField(index, "dataType", event.target.value)}
            >
              {dataTypes.map((type) => (
                <option key={type}>{type}</option>
              ))}
            </select>
            <select
              value={field.fieldType}
              onChange={(event) => updateField(index, "fieldType", event.target.value)}
            >
              <option>DbColumn</option>
              <option>Aggregate</option>
              <option>RawSql</option>
            </select>
            <input
              value={field.rawSql}
              onChange={(event) => updateField(index, "rawSql", event.target.value)}
              placeholder={field.name || "Expression"}
            />
            <button
              className="icon-button subtle"
              type="button"
              title="Remove field"
              disabled={fields.length === 1}
              onClick={() => removeField(index)}
            >
              <Icon name="trash" size={14} />
            </button>
          </div>
        ))}
      </div>
      <button className="button secondary compact add-field-button" type="button" onClick={addField}>
        <Icon name="plus" size={13} /><span className="action-link-text">Add field</span>
      </button>
    </div>
  );
}
