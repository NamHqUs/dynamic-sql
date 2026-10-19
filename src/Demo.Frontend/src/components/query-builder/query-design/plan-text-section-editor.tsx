import { useState } from "react";
import type { ReactNode } from "react";
import Icon from "../../icon/icon";
import { HighlightedTextArea } from "../plain-text/plain-text";
import type { MetadataEntity } from "./query-builder-slice";

export type PlanTextSection = "Filter" | "Sorts";

type PlanTextSectionEditorProps = {
  section: PlanTextSection;
  metadata: MetadataEntity[];
  entityName: string;
  functionNames: string[];
  disabled: boolean;
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

export function usePlanTextSectionEditor({
  section,
  metadata,
  entityName,
  functionNames,
  disabled,
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

  const control = (
    <div className={`projection-text-control plan-text-mode-control${isEditing ? " is-editing" : ""}`}>
      {isEditing ? (
        <>
          <span className="projection-text-mode-label">Plan text</span>
          <button type="button" className="projection-text-action apply" disabled={disabled || isApplying} onClick={() => void apply()}>
            <span className="action-link-text">Apply</span>
          </button>
          <button
            type="button"
            className="projection-text-action"
            disabled={disabled || isApplying}
            onClick={() => {
              setError("");
              setIsEditing(false);
            }}
          >
            <span className="action-link-text">Cancel</span>
          </button>
        </>
      ) : (
        <button
          type="button"
          className="projection-text-action"
          aria-label={`Edit ${title.toLowerCase()} as plan text`}
          title={`Edit ${title.toLowerCase()} as plan text`}
          disabled={disabled || isLoading}
          onClick={() => void openEditor()}
        >
          <Icon name="file-text" size={12} />
        </button>
      )}
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
        disabled={disabled || isApplying}
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
