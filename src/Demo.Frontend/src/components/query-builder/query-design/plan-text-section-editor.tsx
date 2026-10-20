import { useState } from "react";
import type { ReactNode } from "react";
import Icon from "../../icon/icon";
import type { MetadataEntity } from "./query-builder-slice";

export type PlanTextSection = "Filter" | "Sorts";

type PlanTextSectionEditorProps = {
  section: PlanTextSection;
  metadata: MetadataEntity[];
  entityName: string;
  functionNames: string[];
  disabled: boolean;
  readOnly: boolean;
  onLoadPlanText: () => Promise<string>;
  onApplyPlanText: (section: PlanTextSection, value: string) => Promise<void>;
};

const sectionKeys: Record<PlanTextSection, string[]> = {
  Filter: ["Filter", "Filters"],
  Sorts: ["Sorts"],
};

function findSectionStart(lines: string[], section: PlanTextSection): number {
  return lines.findIndex((line) => {
    const separator = line.indexOf(":");
    return separator > 0
      && sectionKeys[section].includes(line.slice(0, separator).trim());
  });
}

function findNextField(lines: string[], start: number): number {
  for (let index = start + 1; index < lines.length; index += 1) {
    const line = lines[index];
    if (line.length > 0 && !/\s/.test(line[0]) && line.indexOf(":") > 0) return index;
  }
  return lines.length;
}

export function getPlanTextSectionValue(text: string, section: PlanTextSection): string {
  const lines = text.split(/\r?\n/);
  const start = findSectionStart(lines, section);
  if (start < 0) return "";

  const separator = lines[start].indexOf(":");
  const end = findNextField(lines, start);
  return [
    lines[start].slice(separator + 1).replace(/^ /, ""),
    ...lines.slice(start + 1, end).map((line) => line.trimStart()),
  ].join("\n");
}

export function replacePlanTextSectionValue(
  text: string,
  section: PlanTextSection,
  value: string,
): string {
  const lines = text.split(/\r?\n/);
  const start = findSectionStart(lines, section);
  const replacementLines = value.split(/\r?\n/);

  if (start < 0) {
    const insertionIndex = lines.length > 1 && lines[lines.length - 1] === ""
      ? lines.length - 1
      : lines.length;
    lines.splice(
      insertionIndex,
      0,
      `${section}: ${replacementLines[0]}`,
      ...replacementLines.slice(1).map((line) => `  ${line}`),
    );
  } else {
    const separator = lines[start].indexOf(":");
    const fieldName = lines[start].slice(0, separator).trim();
    const end = findNextField(lines, start);
    lines.splice(
      start,
      end - start,
      `${fieldName}: ${replacementLines[0]}`,
      ...replacementLines.slice(1).map((line) => `  ${line}`),
    );
  }

  return lines.join("\n");
}

function getEntityFieldNames(metadata: MetadataEntity[], entityName: string): Set<string> {
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
  const entity = metadata.find((item) => item.name === entityName);
  if (entity) addEntityFields(entity);
  return fieldNames;
}

function createTextHighlighter(fieldNames: string[], functionNames: string[]) {
  const escapeRegExp = (value: string) => value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const alternatives = (values: string[]) => [...new Set(values)]
    .filter(Boolean)
    .sort((left, right) => right.length - left.length)
    .map(escapeRegExp)
    .join("|");
  const tokenPatterns = [
    alternatives(functionNames) && {
      type: "function",
      pattern: `(?<![\\w.])(${alternatives(functionNames)})(?=\\s*\\()`,
    },
    alternatives(fieldNames) && {
      type: "field",
      pattern: `(?<![\\w.])(${alternatives(fieldNames)})(?![\\w.])`,
    },
  ].filter((entry): entry is { type: string; pattern: string } => Boolean(entry));
  const highlightPattern = tokenPatterns.length
    ? new RegExp(tokenPatterns.map(({ pattern }) => pattern).join("|"), "gi")
    : null;

  return (value: string): ReactNode => {
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
      parts.push(<span className={`query-request-structure-${className}`} key={matchIndex}>{token}</span>);
      previousIndex = matchIndex + token.length;
    }
    if (previousIndex < value.length) parts.push(value.slice(previousIndex));
    return parts;
  };
}

function syncHighlightScroll(element: HTMLTextAreaElement) {
  const highlight = element.parentElement?.querySelector<HTMLDivElement>(".query-request-structure-highlight");
  if (highlight) {
    highlight.scrollLeft = element.scrollLeft;
    highlight.scrollTop = element.scrollTop;
  }
}

export function HighlightedTextArea({
  metadata,
  entityName,
  functionNames,
  value,
  ariaLabel,
  disabled,
  placeholder,
  onChange,
}: {
  metadata: MetadataEntity[];
  entityName: string;
  functionNames: string[];
  value: string;
  ariaLabel: string;
  disabled: boolean;
  placeholder: string;
  onChange: (value: string) => void;
}) {
  const fieldNames = getEntityFieldNames(metadata, entityName);
  const renderHighlightedText = createTextHighlighter([...fieldNames], functionNames);

  return (
    <div className="query-request-structure-input-container query-request-structure-input-container-multiline projection-text-area-container">
      <div className="query-request-structure-highlight" aria-hidden="true">{renderHighlightedText(value)}</div>
      <textarea
        className="query-request-structure-input query-request-structure-input-highlighted query-request-structure-input-multiline"
        aria-label={ariaLabel}
        spellCheck={false}
        placeholder={placeholder}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        onScroll={(event) => syncHighlightScroll(event.currentTarget)}
        disabled={disabled}
      />
    </div>
  );
}

export function usePlanTextSectionEditor({
  section,
  metadata,
  entityName,
  functionNames,
  disabled,
  readOnly,
  onLoadPlanText,
  onApplyPlanText,
}: PlanTextSectionEditorProps): { control: ReactNode; editor: ReactNode } {
  const [isEditing, setIsEditing] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [isApplying, setIsApplying] = useState(false);
  const [value, setValue] = useState("");
  const [error, setError] = useState("");
  const title = section === "Filter" ? "Filters" : "Sorts";

  const openEditor = async () => {
    setIsLoading(true);
    setError("");
    try {
      const text = await onLoadPlanText();
      setValue(getPlanTextSectionValue(text, section));
      setIsEditing(true);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : String(loadError));
    } finally {
      setIsLoading(false);
    }
  };

  const apply = async () => {
    setIsApplying(true);
    setError("");
    try {
      await onApplyPlanText(section, value);
      setIsEditing(false);
    } catch (applyError) {
      setError(applyError instanceof Error ? applyError.message : String(applyError));
    } finally {
      setIsApplying(false);
    }
  };

  const toggleEditor = async () => {
    if (isEditing) {
      if (readOnly) {
        setIsEditing(false);
        return;
      }
      await apply();
      return;
    }
    await openEditor();
  };

  const control = (
    <div className={`projection-text-control plan-text-mode-control${isEditing ? " is-editing" : ""}`}>
      <button
        type="button"
        className="projection-text-action"
        aria-label={isEditing ? `Show ${title.toLowerCase()} design` : `Edit ${title.toLowerCase()} as plan text`}
        aria-expanded={isEditing}
        title={isEditing ? `Show ${title.toLowerCase()} design` : `Edit ${title.toLowerCase()} as plan text`}
        disabled={disabled || isLoading || isApplying}
        onClick={() => void toggleEditor()}
      >
        <Icon name="file-text" size={14} />
      </button>
    </div>
  );

  const editor = isEditing ? (
    <div className="section-plan-text-editor">
      <HighlightedTextArea
        metadata={metadata}
        entityName={entityName}
        functionNames={functionNames}
        value={value}
        ariaLabel={`${title} plan text`}
        disabled={disabled || readOnly || isApplying}
        placeholder={`Enter ${title.toLowerCase()} plan text`}
        onChange={setValue}
      />
      {error && <p className="query-message error" role="alert">{error}</p>}
    </div>
  ) : error ? (
    <p className="query-message error" role="alert">{error}</p>
  ) : null;

  return { control, editor };
}
