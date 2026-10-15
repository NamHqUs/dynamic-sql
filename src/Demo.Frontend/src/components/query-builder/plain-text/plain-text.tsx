import "./plain-text.css";

type PlainTextProps = {
  id: string;
  labelledBy: string;
  status: "idle" | "loading" | "succeeded" | "failed";
  error: string;
  text: string;
  dirty: boolean;
  disabled: boolean;
  onFieldChange: (index: number, value: string) => void;
};

export default function PlainText({
  id,
  labelledBy,
  status,
  error,
  text,
  dirty,
  disabled,
  onFieldChange,
}: PlainTextProps) {
  const fields = text.split(/\r?\n/).map((line, index) => {
    const separator = line.indexOf(":");
    return separator < 0
      ? { label: "", value: line, index }
      : {
        label: line.slice(0, separator).trim(),
        value: line.slice(separator + 1).replace(/^ /, ""),
        index,
      };
  }).filter((field) => field.label);

  return (
    <div id={id} className="query-request-structure" role="tabpanel" aria-labelledby={labelledBy}>
      {status === "loading" && <p className="query-message">Loading request structure…</p>}
      {status === "failed" && <p className="query-message error" role="alert">{error}</p>}
      {status === "succeeded" && (
        <div className="query-request-structure-content">
          {dirty && <div className="query-request-structure-actions">Plan text has unapplied changes.</div>}
          {error && <p className="query-message error" role="alert">{error}</p>}
          {fields.map(({ label, value, index }) => (
            <div className="query-request-structure-line" key={label}>
              <span className="query-request-structure-key">{label}:</span>
              {label === "Entity" ? (
                <span className="query-request-structure-entity">{value}</span>
              ) : label === "IncludeArchive" ? (
                <input
                  className="query-request-structure-checkbox"
                  type="checkbox"
                  aria-label={label}
                  checked={value.trim().toLowerCase() === "true"}
                  onChange={(event) => onFieldChange(index, event.target.checked ? "True" : "False")}
                  disabled={disabled}
                />
              ) : (
                <input
                  className="query-request-structure-input"
                  aria-label={label}
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
