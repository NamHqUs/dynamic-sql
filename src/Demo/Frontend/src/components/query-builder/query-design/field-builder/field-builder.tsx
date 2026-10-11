import { useState } from "react";
import FieldBox, { resolveEntity } from "./field-box/field-box";
import { MetadataEntity } from "../query-builder-slice";
import Icon from "../../../icon/icon";
import "./field-builder.css";

const FIELD_BOX_HEIGHT = 34;

type FieldBuilderProps = {
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  value: string;
  onChange: (path: string) => void;
  onRemove?: () => void;
  showPath?: boolean;
  showDataType?: boolean;
  placeholder?: string;
  ariaLabel?: string;
  excludedPaths?: string[];
  editMode?: boolean;
  onCancel?: () => void;
};

function getFieldDataType(entity: MetadataEntity, metadata: MetadataEntity[], value: string) {
  const segments = value.split(".");
  const lastSegment = segments[segments.length - 1] ?? "";
  const aggregateField = lastSegment.match(/^(Count\(\)|Sum|Max|Min|Average)\(([^)]*)\)$/)?.[2];
  if (lastSegment === "Count()" || aggregateField) return "Number";

  const owner = resolveEntity(entity, metadata, segments.slice(0, -1));
  return owner?.fields.find((field) => field.name === lastSegment)?.dataType;
}

export default function FieldBuilder({
  entity,
  metadata,
  value,
  onChange,
  onRemove,
  showPath = false,
  showDataType = false,
  placeholder = "Select field…",
  ariaLabel = "Field",
  excludedPaths = [],
  editMode = false,
  onCancel,
}: FieldBuilderProps) {
  const [isEditing, setIsEditing] = useState(editMode);
  const cancelEdit = () => {
    setIsEditing(false);
    onCancel?.();
  };

  if (!value || editMode || isEditing) {
    return (
      <div
        className="field-builder-editing"
        autoFocus
        tabIndex={-1}
        onKeyDown={(event) => {
          if (event.key === "Escape") {
            event.preventDefault();
            cancelEdit();
          }
        }}
        onBlurCapture={(event) => {
          const nextTarget = event.relatedTarget;
          if (nextTarget instanceof Element && nextTarget.closest(".field-box-menu-popup")) return;
          if (!nextTarget || !(nextTarget instanceof Node) || !event.currentTarget.contains(nextTarget)) {
            cancelEdit();
          }
        }
      }
      >
        <FieldBox
          entity={entity}
          metadata={metadata}
          value={value}
          clearOnSelect
          autoFocus={Boolean(value)}
          showPath={showPath}
          placeholder={placeholder}
          ariaLabel={ariaLabel}
          excludedPaths={excludedPaths}
          onChange={(path) => {
            setIsEditing(false);
            onChange(path);
          }}
        />
      </div>
    );
  }

  const dataType = showDataType ? getFieldDataType(entity, metadata, value) : undefined;
  const displayValue = showPath ? value : value.split(".").pop() ?? value;

  return (
    <div className="field-builder-selected" style={{ height: FIELD_BOX_HEIGHT }}>
      <span
        className="field-builder-value"
        aria-label={`Selected ${ariaLabel.toLowerCase()}: ${value}`}
        onClick={() => setIsEditing(true)}
        role="button"
        tabIndex={0}
        onKeyDown={(event) => {
          if (event.key === "Enter" || event.key === " ") setIsEditing(true);
        }}
      >
        {displayValue}
      </span>
      {dataType && <span className="field-builder-type">{dataType}</span>}
      {onRemove && (
        <button
          type="button"
          className="field-builder-remove"
          aria-label={`Remove ${ariaLabel.toLowerCase()}`}
          title={`Remove ${ariaLabel.toLowerCase()}`}
          onClick={onRemove}
        >
          <Icon name="close" size={12} />
        </button>
      )}
    </div>
  );
}
