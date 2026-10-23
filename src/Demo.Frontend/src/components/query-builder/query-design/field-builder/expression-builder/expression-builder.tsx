import { useEffect, useMemo, useRef, useState } from "react";
import type { PointerEvent as ReactPointerEvent } from "react";
import type {
  IFieldDefinition,
  MetadataEntity,
} from "../../query-builder-slice";
import { FieldBoxPopupContext } from "../field-box/field-box-popup-context";
import Icon from "../../../../icon/icon";
import type { FunctionDefinition } from "../../../../metadata-builder/function-definition/types";
import NodeEditor from "./expression-node-editor";
import {
  adjustExpressionPathAfterRemoval,
  createExpression,
  formatExpression,
  getExpressionDropTarget,
  getExpressionError,
  getExpressionNode,
  getGroupArgumentOperator,
  isExpressionGroup,
  isSameExpressionPath,
  removeExpressionNode,
  replaceExpressionNode,
  setExpressionOperation,
  updateExpressionGroup,
  type ExpressionPath,
  type ExpressionPointerDrag,
} from "./expression-model";
import "./expression-builder.css";

export { formatExpression } from "./expression-model";

type ExpressionBuilderProps = {
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  functionDefinitions: FunctionDefinition[];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  initialKind?: "group" | "function";
  initialExpression?: IFieldDefinition;
  onApply: (expression: IFieldDefinition) => void;
};

export default function ExpressionBuilder({
  entity,
  metadata,
  functionDefinitions,
  functionDefinitionsStatus,
  functionDefinitionsError,
  initialKind,
  initialExpression,
  onApply,
}: ExpressionBuilderProps) {
  const [activeFieldBoxPopupId, setActiveFieldBoxPopupId] = useState<symbol | null>(null);
  const popupManager = useMemo(
    () => ({ activeMenuId: activeFieldBoxPopupId, setActiveMenuId: setActiveFieldBoxPopupId }),
    [activeFieldBoxPopupId],
  );
  const [expression, setExpression] = useState<IFieldDefinition>(
    initialExpression ?? createExpression(initialKind === "function" ? "function" : "group", functionDefinitions),
  );
  const [selectedPath, setSelectedPath] = useState<ExpressionPath>();
  const expressionBuilderRef = useRef<HTMLDivElement>(null);
  const [pointerDrag, setPointerDrag] = useState<ExpressionPointerDrag>();
  const [dragPreview, setDragPreview] = useState<ExpressionPath>();
  const [dragPosition, setDragPosition] = useState<"before" | "after">();
  const [copyFeedback, setCopyFeedback] = useState<{
    json: string;
    status: "copied" | "error";
  }>();
  const modelJson = JSON.stringify(expression, null, 2);
  const copyStatus = copyFeedback?.json === modelJson ? copyFeedback.status : undefined;
  const validationError = getExpressionError(expression);

  const copyModelJson = async () => {
    try {
      await navigator.clipboard.writeText(modelJson);
      setCopyFeedback({ json: modelJson, status: "copied" });
    } catch {
      setCopyFeedback({ json: modelJson, status: "error" });
    }
  };

  const groupNodeOnField = (
    sourcePath: ExpressionPath,
    targetPath: ExpressionPath,
    position: "before" | "after",
  ) => {
    if (!sourcePath.length || !targetPath.length || isSameExpressionPath(sourcePath, targetPath)
      || (sourcePath.length < targetPath.length
        && sourcePath.every((index, pathIndex) => index === targetPath[pathIndex]))) return;
    const sourceNode = getExpressionNode(expression, sourcePath);
    const sourceParentPath = sourcePath.slice(0, -1);
    const sourceParent = getExpressionNode(expression, sourceParentPath);
    const sourceIndex = sourcePath[sourcePath.length - 1];
    const targetNode = getExpressionNode(expression, targetPath);
    const targetParent = getExpressionNode(expression, targetPath.slice(0, -1));
    const targetIndex = targetPath[targetPath.length - 1];
    if (!sourceNode || !isExpressionGroup(sourceParent) || sourceParent.arguments.length <= 1
      || sourceIndex === undefined || !targetNode || !isExpressionGroup(targetParent)
      || targetIndex === undefined) return;
    const targetField = targetNode.kind === "math" ? targetNode.field : targetNode;
    if (targetField.kind !== "field") return;

    const sourceOperation = getGroupArgumentOperator(sourceParent, sourceNode, sourceIndex);
    const targetOperation = getGroupArgumentOperator(targetParent, targetNode, targetIndex);
    const sourceComesFirst = position === "before";
    const removed = updateExpressionGroup(expression, sourceParentPath, (group) => ({
      ...group,
      arguments: group.arguments.filter((_, index) => index !== sourceIndex),
    }));
    const adjustedTargetPath = adjustExpressionPathAfterRemoval(sourcePath, targetPath);
    const adjustedTarget = getExpressionNode(removed, adjustedTargetPath);
    if (!adjustedTarget) return;
    const adjustedTargetField = adjustedTarget.kind === "math" ? adjustedTarget.field : adjustedTarget;
    if (adjustedTargetField.kind !== "field") return;
    const sourceField = sourceNode.kind === "math" ? sourceNode.field : sourceNode;
    const firstField = sourceComesFirst ? sourceField : adjustedTargetField;
    const secondField = sourceComesFirst ? adjustedTargetField : sourceField;
    const firstOperation = sourceComesFirst ? sourceOperation : targetOperation;
    const secondOperation = sourceComesFirst ? targetOperation : sourceOperation;
    const grouped: IFieldDefinition = {
      kind: "group",
      ...(firstOperation ? { operator: firstOperation } : {}),
      arguments: [
        firstField,
        setExpressionOperation(secondField, secondOperation),
      ],
    };
    setExpression(replaceExpressionNode(removed, adjustedTargetPath, grouped));
  };

  const moveNode = (sourcePath: ExpressionPath, targetPath: ExpressionPath) => {
    if (!sourcePath.length || sourcePath.length <= targetPath.length
      && sourcePath.every((index, pathIndex) => index === targetPath[pathIndex])) return;
    const node = getExpressionNode(expression, sourcePath);
    if (!node) return;
    const removed = updateExpressionGroup(expression, sourcePath.slice(0, -1), (parent) => ({
      ...parent,
      arguments: parent.arguments.filter((_, index) => index !== sourcePath[sourcePath.length - 1]),
    }));
    const adjustedTargetPath = adjustExpressionPathAfterRemoval(sourcePath, targetPath);
    const target = getExpressionNode(removed, adjustedTargetPath);
    if (!target || !isExpressionGroup(target)) return;
    const lastIndex = target.arguments.length - 1;
    const operation = lastIndex < 0
      ? null
      : target.operator
        ?? getGroupArgumentOperator(target, target.arguments[lastIndex]!, lastIndex)
        ?? "+";
    const moved = setExpressionOperation(node, operation);
    setExpression(updateExpressionGroup(removed, adjustedTargetPath, (group) => ({
      ...group,
      arguments: [...group.arguments, moved],
    })));
  };

  const startPointerDrag = (event: ReactPointerEvent<HTMLDivElement>, sourcePath: ExpressionPath) => {
    if (event.button !== 0) return;
    event.preventDefault();
    event.stopPropagation();
    event.currentTarget.setPointerCapture(event.pointerId);
    setPointerDrag({ sourcePath, x: event.clientX, y: event.clientY });
    setDragPreview(undefined);
    setDragPosition(undefined);
  };

  const updatePointerDrag = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!pointerDrag) return;
    event.preventDefault();
    const target = getExpressionDropTarget(event.clientX, event.clientY);
    setPointerDrag({ ...pointerDrag, x: event.clientX, y: event.clientY });
    setDragPreview(target?.path);
    setDragPosition(target?.kind === "field" ? target.position : undefined);
  };

  const finishPointerDrag = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!pointerDrag) return;
    const target = getExpressionDropTarget(event.clientX, event.clientY);
    if (target?.kind === "field") {
      groupNodeOnField(pointerDrag.sourcePath, target.path, target.position);
    } else if (target?.kind === "group") {
      moveNode(pointerDrag.sourcePath, target.path);
    }
    setPointerDrag(undefined);
    setDragPreview(undefined);
    setDragPosition(undefined);
  };

  const cancelPointerDrag = () => {
    setPointerDrag(undefined);
    setDragPreview(undefined);
    setDragPosition(undefined);
  };

  useEffect(() => {
    if (selectedPath === undefined) return;

    const deleteSelectedExpression = (event: KeyboardEvent) => {
      if (event.key !== "Delete" || event.defaultPrevented) return;
      const target = event.target;
      if (
        target instanceof HTMLElement
        && (target.isContentEditable || target.closest("input, textarea, select, [contenteditable='true']"))
      ) return;

      const isInsideEditor = target instanceof Node && expressionBuilderRef.current?.contains(target);
      if (!isInsideEditor && document.activeElement !== document.body) return;

      event.preventDefault();
      setExpression((current) => removeExpressionNode(current, selectedPath));
      setSelectedPath(undefined);
    };
    const clearSelectionOutsideEditor = (event: PointerEvent) => {
      const target = event.target;
      if (target instanceof Node && expressionBuilderRef.current?.contains(target)) return;
      if (target instanceof Element && target.closest(".field-box-menu-popup")) return;
      setSelectedPath(undefined);
    };

    document.addEventListener("keydown", deleteSelectedExpression);
    document.addEventListener("pointerdown", clearSelectionOutsideEditor);
    return () => {
      document.removeEventListener("keydown", deleteSelectedExpression);
      document.removeEventListener("pointerdown", clearSelectionOutsideEditor);
    };
  }, [selectedPath]);

  const draggedNode = pointerDrag ? getExpressionNode(expression, pointerDrag.sourcePath) : undefined;

  return (
    <FieldBoxPopupContext.Provider value={popupManager}>
      <div
        ref={expressionBuilderRef}
        className="expression-builder"
        onClick={() => setSelectedPath(undefined)}
        onPointerMove={updatePointerDrag}
        onPointerUp={finishPointerDrag}
        onPointerCancel={cancelPointerDrag}
      >
        <NodeEditor
          expression={expression}
          entity={entity}
          metadata={metadata}
          functionDefinitions={functionDefinitions}
          functionDefinitionsStatus={functionDefinitionsStatus}
          functionDefinitionsError={functionDefinitionsError}
          path={[]}
          onPointerDragStart={startPointerDrag}
          dragPreview={dragPreview}
          dragPosition={dragPosition}
          onChange={setExpression}
          selectionPath={[]}
          selectedPath={selectedPath}
          onSelect={setSelectedPath}
          root
        />

        {pointerDrag && draggedNode && (
          <div
            className="expression-drag-preview"
            style={{ left: pointerDrag.x + 12, top: pointerDrag.y + 12 }}
            aria-hidden="true"
          >
            {formatExpression(draggedNode)}
          </div>
        )}

        <div className="expression-preview" aria-live="polite">
          <span className="expression-preview-label">PREVIEW</span>
          <code>{formatExpression(expression)}</code>
          <div className="expression-preview-json-heading">
            <span className="expression-preview-label">MODEL JSON</span>
            <button
              type="button"
              className="expression-preview-copy"
              aria-label="Copy JSON model"
              title="Copy JSON model"
              onClick={copyModelJson}
            >
              <Icon name={copyStatus === "copied" ? "check" : "copy"} size={13} />
            </button>
            {copyStatus && (
              <span className={`expression-preview-copy-status${copyStatus === "error" ? " expression-preview-copy-error" : ""}`} role="status">
                {copyStatus === "copied" ? "Copied" : "Copy failed. Check clipboard permissions."}
              </span>
            )}
          </div>
          <pre className="expression-preview-json"><code>{modelJson}</code></pre>
        </div>

        <footer className="expression-builder-footer">
          <div className="expression-builder-actions">
            <button
              type="button"
              className="expression-apply-button"
              disabled={Boolean(validationError)}
              onClick={() => onApply(expression)}
            >
              <Icon name="check" size={14} />
              Apply
            </button>
          </div>
        </footer>
      </div>
    </FieldBoxPopupContext.Provider>
  );
}
