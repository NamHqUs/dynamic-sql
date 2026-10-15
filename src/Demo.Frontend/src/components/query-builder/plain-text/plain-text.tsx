import "./plain-text.css";
import { MetadataEntity } from "../query-design/query-builder-slice";

type PlainTextProps = {
  id: string;
  labelledBy: string;
  metadata: MetadataEntity[];
  status: "idle" | "loading" | "succeeded" | "failed";
  error: string;
  text: string;
  disabled: boolean;
  onFieldChange: (index: number, value: string) => void;
};

export default function PlainText({
  id,
  labelledBy,
  metadata,
  status,
  error,
  text,
  disabled,
  onFieldChange,
}: PlainTextProps) {
  const fields: { label: string; value: string; index: number }[] = [];
  text.split(/\r?\n/).forEach((line, index) => {
    const separator = line.indexOf(":");
    if (separator < 0) {
      const previousField = fields[fields.length - 1];
      if (previousField) previousField.value += `\n${line}`;
      return;
    }
    fields.push({
      label: line.slice(0, separator).trim(),
      value: line.slice(separator + 1).replace(/^ /, ""),
      index,
    });
  });

  const syncHighlightScroll = (element: HTMLInputElement | HTMLTextAreaElement) => {
    const highlight = element.parentElement?.querySelector<HTMLDivElement>(
      ".query-request-structure-highlight",
    );
    if (highlight) {
      highlight.scrollLeft = element.scrollLeft;
      highlight.scrollTop = element.scrollTop;
    }
  };

  return (
    <div id={id} className="query-request-structure" role="tabpanel" aria-labelledby={labelledBy}>
      {status === "loading" && <p className="query-message">Loading request structure…</p>}
      {status === "failed" && <p className="query-message error" role="alert">{error}</p>}
      {status === "succeeded" && (
        <div className="query-request-structure-content">
          {error && <p className="query-message error" role="alert">{error}</p>}
          {fields.map(({ label, value, index }) => (
            <div className="query-request-structure-line" key={label}>
              <span className="query-request-structure-key">{label}:</span>
              {label === "Entity" ? (
                <select
                  className="query-request-structure-entity"
                  aria-label={label}
                  value={value}
                  onChange={(event) => {
                    const entityName = event.target.value;
                    const selectedEntity = metadata.find((entity) => entity.name === entityName);
                    onFieldChange(index, entityName);
                    const fieldsLine = fields.find((field) => field.label === "Fields");
                    if (selectedEntity && fieldsLine) {
                      onFieldChange(fieldsLine.index, selectedEntity.fields.map((field) => field.name).join(", "));
                    }
                  }}
                  disabled={disabled}
                >
                  {value && !metadata.some((entity) => entity.name === value) && (
                    <option value={value}>{value}</option>
                  )}
                  {metadata.map((entity) => (
                    <option value={entity.name} key={entity.name}>{entity.name}</option>
                  ))}
                </select>
              ) : label === "IncludeArchive" ? (
                <input
                  className="query-request-structure-checkbox"
                  type="checkbox"
                  aria-label={label}
                  checked={value.trim().toLowerCase() === "true"}
                  onChange={(event) => onFieldChange(index, event.target.checked ? "True" : "False")}
                  disabled={disabled}
                />
              ) : ["Fields", "Filters", "Sorts"].includes(label) ? (
                <div className={`query-request-structure-input-container${label === "Fields" || label === "Filters" ? " query-request-structure-input-container-multiline" : ""}`}>
                  <div className="query-request-structure-highlight" aria-hidden="true">
                    {value.split(/(\b[A-Za-z_]\w*(?=\s*\())/g).map((part, partIndex) => (
                      partIndex % 2 === 1
                        ? <span className="query-request-structure-function" key={partIndex}>{part}</span>
                        : part
                    ))}
                  </div>
                  {label === "Fields" || label === "Filters" ? (
                    <textarea
                      className="query-request-structure-input query-request-structure-input-highlighted query-request-structure-input-multiline"
                      aria-label={label}
                      rows={3}
                      spellCheck={false}
                      ref={(element) => {
                        if (!element) return;
                        element.style.height = "auto";
                        element.style.height = `${element.scrollHeight}px`;
                      }}
                      value={value}
                      onChange={(event) => onFieldChange(index, event.target.value)}
                      onScroll={(event) => syncHighlightScroll(event.currentTarget)}
                      disabled={disabled}
                    />
                  ) : (
                    <input
                      className="query-request-structure-input query-request-structure-input-highlighted"
                      aria-label={label}
                      spellCheck={false}
                      value={value}
                      onChange={(event) => onFieldChange(index, event.target.value)}
                      onScroll={(event) => syncHighlightScroll(event.currentTarget)}
                      disabled={disabled}
                    />
                  )}
                </div>
              ) : (
                <input
                  className="query-request-structure-input"
                  aria-label={label}
                  spellCheck={false}
                  value={value}
                  onChange={(event) => onFieldChange(index, event.target.value)}
                  disabled={disabled}
                />
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
