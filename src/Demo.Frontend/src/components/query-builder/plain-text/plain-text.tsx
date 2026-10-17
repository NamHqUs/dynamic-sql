import "./plain-text.css";
import type { ReactNode } from "react";
import { MetadataEntity } from "../query-design/query-builder-slice";

type MultilineField = "Entities" | "Fields" | "Filters";

type PlainTextProps = {
  id: string;
  labelledBy: string;
  metadata: MetadataEntity[];
  status: "idle" | "loading" | "succeeded" | "failed";
  error: string;
  text: string;
  disabled: boolean;
  textareaHeights: Record<MultilineField, number>;
  onTextareaHeightChange: (field: MultilineField, height: number) => void;
  onFieldChange: (index: number, value: string) => void;
  functionNames: string[];
  functionNamesStatus: "idle" | "loading" | "succeeded" | "failed";
  functionNamesError: string;
};

export default function PlainText({
  id,
  labelledBy,
  metadata,
  status,
  error,
  text,
  disabled,
  textareaHeights,
  onTextareaHeightChange,
  onFieldChange,
  functionNames,
  functionNamesStatus,
  functionNamesError,
}: PlainTextProps) {
  const fields: { label: string; value: string; index: number }[] = [];
  text.split(/\r?\n/).forEach((line, index) => {
    if (line.length > 0 && /\s/.test(line[0])) {
      const previousField = fields[fields.length - 1];
      if (previousField) previousField.value += `\n${line.trim()}`;
      return;
    }
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
  const escapeRegExp = (value: string) => value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const entityList = fields.find(({ label }) => label === "Entities")?.value;
  const selectedEntityName = fields.find(({ label }) => label === "Entity")?.value
    ?? entityList?.split(/\r?\n/).find((value) => value.trim())?.trim();
  const selectedEntity = metadata.find((entity) => entity.name === selectedEntityName);
  const fieldNames = new Set<string>();
  const addEntityFields = (entity: MetadataEntity, prefix = "", visited = new Set<string>()) => {
    if (visited.has(entity.name)) return;
    const nextVisited = new Set(visited).add(entity.name);
    entity.fields.forEach(({ name }) => fieldNames.add(prefix ? `${prefix}.${name}` : name));
    entity.relations?.forEach((relation) => {
      const relatedEntity = metadata.find((candidate) => candidate.name === relation.relatedEntity);
      if (relatedEntity) addEntityFields(relatedEntity, prefix ? `${prefix}.${relation.name}` : relation.name, nextVisited);
    });
  };
  if (selectedEntity) addEntityFields(selectedEntity);

  const alternatives = (values: string[]) => [...new Set(values)]
    .filter(Boolean)
    .sort((left, right) => right.length - left.length)
    .map(escapeRegExp)
    .join("|");
  const functionAlternatives = alternatives(functionNames);
  const fieldAlternatives = alternatives([...fieldNames]);
  const tokenPatterns = [
    functionAlternatives && {
      type: "function",
      pattern: `(?<![\\w.])(${functionAlternatives})(?=\\s*\\()`,
    },
    fieldAlternatives && {
      type: "field",
      pattern: `(?<![\\w.])(${fieldAlternatives})(?![\\w.])`,
    },
  ].filter((entry): entry is { type: string; pattern: string } => Boolean(entry));
  const highlightPattern = tokenPatterns.length
    ? new RegExp(tokenPatterns.map(({ pattern }) => pattern).join("|"), "gi")
    : null;
  const renderHighlightedText = (value: string) => {
    if (!highlightPattern) return value;

    const parts: ReactNode[] = [];
    let previousIndex = 0;
    highlightPattern.lastIndex = 0;
    for (const match of value.matchAll(highlightPattern)) {
      const token = match[0];
      const matchIndex = match.index ?? 0;
      const tokenType = tokenPatterns.findIndex((_, index) => Boolean(match[index + 1]));
      const className = tokenPatterns[tokenType]?.type ?? "field";
      if (matchIndex > previousIndex) parts.push(value.slice(previousIndex, matchIndex));
      parts.push(
        <span className={`query-request-structure-${className}`} key={matchIndex}>{token}</span>,
      );
      previousIndex = matchIndex + token.length;
    }
    if (previousIndex < value.length) parts.push(value.slice(previousIndex));
    return parts;
  };
  const fieldsAreVisible = status === "succeeded" || (status === "loading" && text.length > 0);
  const fieldsAreDisabled = disabled || status === "loading";

  return (
    <div id={id} className="query-request-structure" role="tabpanel" aria-labelledby={labelledBy}>
      {status === "failed" && <p className="query-message error" role="alert">{error}</p>}
      {functionNamesStatus === "failed" && (
        <p className="query-message error" role="alert">Function highlighting unavailable: {functionNamesError}</p>
      )}
      {fieldsAreVisible && (
        <div className="query-request-structure-content">
          {error && <p className="query-message error" role="alert">{error}</p>}
          {fields.filter(({ label }) => label.toLowerCase() !== "groupby").map(({ label, value, index }) => (
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
                  disabled={fieldsAreDisabled}
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
                  disabled={fieldsAreDisabled}
                />
              ) : ["Entities", "Fields", "Filters", "Sorts"].includes(label) ? (
                <div className={`query-request-structure-input-container${label === "Entities" || label === "Fields" || label === "Filters" ? " query-request-structure-input-container-multiline" : ""}`}>
                  <div className="query-request-structure-highlight" aria-hidden="true">
                    {renderHighlightedText(value)}
                  </div>
                  {label === "Entities" || label === "Fields" || label === "Filters" ? (
                    <textarea
                      className="query-request-structure-input query-request-structure-input-highlighted query-request-structure-input-multiline"
                      aria-label={label}
                      spellCheck={false}
                      style={{ height: `${textareaHeights[label]}px` }}
                      value={value}
                      onChange={(event) => onFieldChange(index, event.target.value)}
                      onScroll={(event) => syncHighlightScroll(event.currentTarget)}
                      onMouseUp={(event) => onTextareaHeightChange(label, event.currentTarget.offsetHeight)}
                      disabled={fieldsAreDisabled}
                    />
                  ) : (
                    <input
                      className="query-request-structure-input query-request-structure-input-highlighted"
                      aria-label={label}
                      spellCheck={false}
                      value={value}
                      onChange={(event) => onFieldChange(index, event.target.value)}
                      onScroll={(event) => syncHighlightScroll(event.currentTarget)}
                      disabled={fieldsAreDisabled}
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
                  disabled={fieldsAreDisabled}
                />
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
