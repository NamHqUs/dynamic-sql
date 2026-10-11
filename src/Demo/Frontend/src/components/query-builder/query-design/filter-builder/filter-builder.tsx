import type { CSSProperties, DragEvent } from "react";
import { Fragment, useLayoutEffect, useRef, useState } from "react";
import Icon from "../../../icon/icon";
import { MetadataEntity, QueryDefinition, QueryFilterGroup, QueryFilterItem, QueryFilterNode } from "../query-builder-slice";
import FieldBuilder from "../field-builder/field-builder";

const operators = [
  [0, "="],
  [1, "!="],
  [2, "contains"],
  [3, ">"],
  [4, "<"],
] as const;

type FilterBuilderProps = {
  metadata: MetadataEntity[];
  definition: QueryDefinition;
  entity: MetadataEntity;
  onDefinitionChange: (definition: QueryDefinition) => void;
};

const isGroup = (filter: QueryFilterNode): filter is QueryFilterGroup => "filters" in filter;

function createFilter(logicalOperator: number): QueryFilterItem {
  return {
    field: "",
    operation: 0,
    value: "",
    parameterized: false,
    logicalOperator,
    enabled: true,
  };
}

function updateGroup(group: QueryFilterGroup, path: FilterPath, update: (group: QueryFilterGroup) => QueryFilterGroup): QueryFilterGroup {
  if (!path.length) return update(group);
  const [index, ...rest] = path;
  const child = group.filters[index];
  if (!child || !isGroup(child)) return group;
  return {
    ...group,
    filters: group.filters.map((filter, filterIndex) => filterIndex === index
      ? updateGroup(child, rest, update)
      : filter),
  };
}

function createGroup(logicalOperation: number): QueryFilterGroup {
  return { logicalOperation, filters: [] };
}

type FilterPath = number[];
type DragPosition = "inside" | "before" | "after";

type DragPreview = {
  path: FilterPath;
  position: DragPosition;
};

function getDropPosition(event: DragEvent<HTMLElement>): DragPosition {
  const bounds = event.currentTarget.getBoundingClientRect();
  return event.clientY < bounds.top + bounds.height / 2 ? "before" : "after";
}

function startFilterDrag(event: DragEvent<HTMLElement>, path: FilterPath, nodeType: "filter" | "group") {
  event.stopPropagation();
  event.dataTransfer.effectAllowed = "move";
  event.dataTransfer.setData("application/x-filter-path", JSON.stringify(path));
  event.dataTransfer.setData("application/x-filter-node-type", nodeType);
}

function isGroupDrag(event: DragEvent<HTMLElement>): boolean {
  return event.dataTransfer.getData("application/x-filter-node-type") === "group";
}

function isPathPrefix(prefix: FilterPath, path: FilterPath): boolean {
  return prefix.length < path.length && prefix.every((index, pathIndex) => index === path[pathIndex]);
}

function isSamePath(first: FilterPath, second: FilterPath): boolean {
  return first.length === second.length && first.every((index, pathIndex) => index === second[pathIndex]);
}

function adjustPathAfterRemoval(sourcePath: FilterPath, targetPath: FilterPath): FilterPath {
  const adjustedPath = [...targetPath];
  const sharedDepth = Math.min(sourcePath.length, targetPath.length);
  for (let depth = 0; depth < sharedDepth; depth++) {
    const sourceIndex = sourcePath[depth];
    const targetIndex = targetPath[depth];
    if (sourceIndex === undefined || targetIndex === undefined) break;
    if (depth > 0 && !sourcePath.slice(0, depth).every((index, pathIndex) => index === targetPath[pathIndex])) return adjustedPath;
    if (sourceIndex < targetIndex) {
      adjustedPath[depth]--;
      return adjustedPath;
    }
    if (sourceIndex > targetIndex) return adjustedPath;
  }
  return adjustedPath;
}

function getNode(group: QueryFilterGroup, path: FilterPath): QueryFilterNode | undefined {
  let node: QueryFilterNode = group;
  for (const index of path) {
    if (!isGroup(node) || !node.filters[index]) return undefined;
    node = node.filters[index];
  }
  return node;
}

function removeNode(group: QueryFilterGroup, path: FilterPath): { group: QueryFilterGroup; node?: QueryFilterNode } {
  if (!path.length) return { group };
  const [index, ...rest] = path;
  if (!rest.length) {
    const node = group.filters[index];
    return {
      group: { ...group, filters: group.filters.filter((_, filterIndex) => filterIndex !== index) },
      node,
    };
  }

  const child = group.filters[index];
  if (!child || !isGroup(child)) return { group };
  const result = removeNode(child, rest);
  return {
    group: {
      ...group,
      filters: group.filters.map((filter, filterIndex) => filterIndex === index ? result.group : filter),
    },
    node: result.node,
  };
}

function appendToGroup(group: QueryFilterGroup, path: FilterPath, node: QueryFilterNode): QueryFilterGroup {
  if (!path.length) return { ...group, filters: [...group.filters, node] };
  const [index, ...rest] = path;
  const child = group.filters[index];
  if (!child || !isGroup(child)) return group;
  return {
    ...group,
    filters: group.filters.map((filter, filterIndex) => filterIndex === index
      ? appendToGroup(child, rest, node)
      : filter),
  };
}

function replaceNode(group: QueryFilterGroup, path: FilterPath, node: QueryFilterNode): QueryFilterGroup {
  if (!path.length) return group;
  const [index, ...rest] = path;
  if (!rest.length) {
    return {
      ...group,
      filters: group.filters.map((filter, filterIndex) => filterIndex === index ? node : filter),
    };
  }
  const child = group.filters[index];
  if (!child || !isGroup(child)) return group;
  return {
    ...group,
    filters: group.filters.map((filter, filterIndex) => filterIndex === index
      ? replaceNode(child, rest, node)
      : filter),
  };
}

type FilterGroupEditorProps = {
  group: QueryFilterGroup;
  metadata: MetadataEntity[];
  entity: MetadataEntity;
  onChange: (group: QueryFilterGroup) => void;
  onRemove?: () => void;
  onMoveFilter: (sourcePath: FilterPath, targetPath: FilterPath, position?: DragPosition) => void;
  onDragPreview: (path: FilterPath, position: DragPosition) => void;
  onDragPreviewClear: () => void;
  dragPreview?: DragPreview;
  onGroupFilter: (path: FilterPath) => void;
  onAddFilter: (path: FilterPath) => void;
  onUngroupFilter: (path: FilterPath) => void;
  path: FilterPath;
  showLogicalOperator: boolean;
  depth: number;
};

function FilterGroupEditor({ group, metadata, entity, onChange, onRemove, onMoveFilter, onDragPreview, onDragPreviewClear, dragPreview, onGroupFilter, onAddFilter, onUngroupFilter, path, showLogicalOperator, depth }: FilterGroupEditorProps) {
  const updateNode = (index: number, node: QueryFilterNode) => {
    const filters = group.filters.map((filter, filterIndex) => filterIndex === index ? node : filter);
    onChange({ ...group, filters });
  };

  const removeNode = (index: number) => {
    onChange({ ...group, filters: group.filters.filter((_, filterIndex) => filterIndex !== index) });
  };

  const addFilter = () => {
    onChange({ ...group, filters: [...group.filters, createFilter(group.filters.length ? group.logicalOperation : 0)] });
  };

  const handleDrop = (event: DragEvent<HTMLDivElement>, position: DragPosition = "inside", targetPath = path) => {
    event.preventDefault();
    event.stopPropagation();
    onDragPreviewClear();
    const serializedPath = event.dataTransfer.getData("application/x-filter-path");
    if (!serializedPath) return;
    try {
      const sourcePath = JSON.parse(serializedPath) as unknown;
      if (Array.isArray(sourcePath) && sourcePath.every((index) => Number.isInteger(index))) {
        onMoveFilter(sourcePath, targetPath, position);
      }
    } catch {
      // Ignore malformed drag payloads from outside this builder.
    }
  };
  const isPreview = (position: DragPosition) => dragPreview?.position === position && isSamePath(dragPreview.path, path);

  return (
    <div
      className={`filter-group filter-group-depth-${Math.min(depth, 3)}${isPreview("inside") ? " filter-drop-preview-inside" : ""}`}
      onDragOver={(event) => event.preventDefault()}
      onDragEnter={() => onDragPreview(path, "inside")}
      onDrop={handleDrop}
    >
      {depth > 0 && (
        <span
          className="filter-group-drag-handle"
          draggable
          aria-hidden="true"
          onDragStart={(event) => startFilterDrag(event, path, "group")}
          onDragEnd={onDragPreviewClear}
        />
      )}
      {depth > 0 && (
        <div
          className={`filter-group-edge-drop filter-group-edge-drop-before${isPreview("before") ? " filter-drop-preview" : ""}`}
          onDragEnter={(event) => {
            event.preventDefault();
            event.stopPropagation();
            onDragPreview(path, isGroupDrag(event) ? "inside" : "before");
          }}
          onDragOver={(event) => {
            event.preventDefault();
            event.stopPropagation();
            onDragPreview(path, isGroupDrag(event) ? "inside" : "before");
          }}
          onDrop={(event) => handleDrop(event, isGroupDrag(event) ? "inside" : "before")}
        />
      )}
      {depth === 0 && (
        <div
          className={`filter-drop-zone filter-drop-zone-before filter-drop-zone-first${isPreview("before") ? " filter-drop-preview" : ""}`}
          onDragOver={(event) => {
            event.preventDefault();
            onDragPreview(path, "before");
          }}
          onDrop={(event) => handleDrop(event, "before")}
        />
      )}
      <div
        className={`filter-group-header${isPreview("inside") ? " filter-drop-preview-inside" : ""}`}
        onDragOver={(event) => {
          event.preventDefault();
          onDragPreview(path, "inside");
        }}
        onDrop={(event) => handleDrop(event, "inside")}
      >
        {depth === 0 && <p className="helper">Build your filters here.</p>}
        {depth > 0 && showLogicalOperator && (
          <label className="filter-group-logic">
            <select
              className="filter-group-logic-select"
              aria-label="Group logical operator"
              value={group.logicalOperation}
              onChange={(event) => onChange({ ...group, logicalOperation: Number(event.target.value) })}
            >
              <option value={0}>AND</option>
              <option value={1}>OR</option>
            </select>
          </label>
        )}
        {onRemove && <>
          <button className="icon-button filter-group-action" type="button" aria-label="Ungroup filters" title="Ungroup filters" onClick={() => onUngroupFilter(path)}><Icon name="ungroup" size={13} /></button>
          <button className="icon-button filter-group-action" type="button" aria-label="Remove filter group" title="Remove filter group" onClick={onRemove}><Icon name="trash" size={13} /></button>
        </>}
      </div>

      {!group.filters.length && <p className="empty-builder">Add a condition or drag one here.</p>}
      {group.filters.map((filter, index) => isGroup(filter) ? (
        <Fragment key={`group-${index}`}>
          <div
            className={`filter-drop-zone filter-drop-zone-before${isSamePath(dragPreview?.path ?? [], [...path, index]) && dragPreview?.position === "before" ? " filter-drop-preview" : ""}`}
            onDragOver={(event) => {
              event.preventDefault();
              onDragPreview([...path, index], isGroupDrag(event) ? "inside" : "before");
            }}
            onDrop={(event) => handleDrop(event, isGroupDrag(event) ? "inside" : "before", [...path, index])}
          />
          <FilterGroupEditor
            group={filter}
            metadata={metadata}
            entity={entity}
            depth={depth + 1}
            path={[...path, index]}
            onMoveFilter={onMoveFilter}
            onDragPreview={onDragPreview}
            onDragPreviewClear={onDragPreviewClear}
            dragPreview={dragPreview}
            onGroupFilter={onGroupFilter}
            onAddFilter={onAddFilter}
            onUngroupFilter={onUngroupFilter}
            showLogicalOperator={index !== 0}
            onChange={(nested) => updateNode(index, nested)}
            onRemove={() => removeNode(index)}
          />
          <div
            className={`filter-drop-zone filter-drop-zone-after${isSamePath(dragPreview?.path ?? [], [...path, index]) && dragPreview?.position === "after" ? " filter-drop-preview" : ""}`}
            onDragOver={(event) => {
              event.preventDefault();
              onDragPreview([...path, index], "after");
            }}
            onDrop={(event) => handleDrop(event, "after", [...path, index])}
          />
        </Fragment>
      ) : (
        <FilterRow
          key={`filter-${index}`}
          filter={filter}
          first={index === 0}
          path={[...path, index]}
          metadata={metadata}
          entity={entity}
          onChange={(next) => updateNode(index, next)}
          onRemove={() => removeNode(index)}
          onGroup={() => onGroupFilter([...path, index])}
          onAdd={() => onAddFilter([...path, index])}
          onMove={(sourcePath, position) => onMoveFilter(sourcePath, [...path, index], position)}
          onDragPreview={onDragPreview}
          onDragPreviewClear={onDragPreviewClear}
          dragPreview={dragPreview}
        />
      ))}
      {depth === 0 && (
        <>
          <button className="text-button filter-add-condition" type="button" onClick={addFilter}><Icon name="plus" size={12} /> Condition</button>
          <div
              className={`filter-drop-zone filter-drop-zone-last${isPreview("after") ? " filter-drop-preview" : ""}`}
              onDragOver={(event) => {
                event.preventDefault();
                onDragPreview(path, "after");
              }}
            onDrop={(event) => handleDrop(event, "after")}
          />
        </>
      )}
      {depth > 0 && (
        <div
          className={`filter-group-edge-drop filter-group-edge-drop-after${isPreview("after") ? " filter-drop-preview" : ""}`}
          onDragEnter={(event) => {
            event.preventDefault();
            event.stopPropagation();
            onDragPreview(path, "after");
          }}
          onDragOver={(event) => {
            event.preventDefault();
            event.stopPropagation();
            onDragPreview(path, "after");
          }}
          onDrop={(event) => handleDrop(event, "after")}
        />
      )}
    </div>
  );
}

type FilterRowProps = {
  filter: QueryFilterItem;
  first: boolean;
  path: FilterPath;
  metadata: MetadataEntity[];
  entity: MetadataEntity;
  onChange: (filter: QueryFilterItem) => void;
  onRemove: () => void;
  onGroup: () => void;
  onAdd: () => void;
  onMove: (sourcePath: FilterPath, position: DragPosition) => void;
  onDragPreview: (path: FilterPath, position: DragPosition) => void;
  onDragPreviewClear: () => void;
  dragPreview?: DragPreview;
};

function FilterRow({ filter, first, path, metadata, entity, onChange, onRemove, onGroup, onAdd, onMove, onDragPreview, onDragPreviewClear, dragPreview }: FilterRowProps) {
  const update = (changes: Partial<QueryFilterItem>) => onChange({ ...filter, ...changes });
  const isPreview = (position: DragPosition) => dragPreview?.position === position && isSamePath(dragPreview.path, path);
  const previewPosition = useRef<DragPosition>();
  const updatePreview = (event: DragEvent<HTMLDivElement>) => {
    const position = getDropPosition(event);
    previewPosition.current = position;
    onDragPreview(path, position);
  };

  return (
    <div
      className={`filter-row${first ? " filter-row-first" : ""}${filter.enabled ? "" : " filter-row-disabled"}${isPreview("before") ? " filter-drop-preview-before" : ""}${isPreview("after") ? " filter-drop-preview-after" : ""}`}
      onDragEnter={(event) => {
        event.preventDefault();
        event.stopPropagation();
        updatePreview(event);
      }}
      onDragOver={(event) => {
        event.preventDefault();
        event.stopPropagation();
        updatePreview(event);
      }}
      onDrop={(event) => {
        event.preventDefault();
        event.stopPropagation();
        const serializedPath = event.dataTransfer.getData("application/x-filter-path");
        if (!serializedPath) return;
        try {
          const sourcePath = JSON.parse(serializedPath) as unknown;
          if (Array.isArray(sourcePath) && sourcePath.every((index) => Number.isInteger(index))) {
            const position = previewPosition.current ?? getDropPosition(event);
            onDragPreviewClear();
            previewPosition.current = undefined;
            onMove(sourcePath, position);
          }
        } catch {
          // Ignore malformed drag payloads from outside this builder.
        }
      }}
    >
      {first ? (
        <select
          className="logical-operator logical-operator-hidden"
          aria-hidden="true"
          tabIndex={-1}
          value={filter.logicalOperator}
          onChange={() => undefined}
        >
          <option value={0}>AND</option>
          <option value={1}>OR</option>
        </select>
      ) : (
        <select
          className="logical-operator"
          aria-label="Logical operator before condition"
          value={filter.logicalOperator}
          onChange={(event) => update({ logicalOperator: Number(event.target.value) })}
        >
          <option value={0}>AND</option>
          <option value={1}>OR</option>
        </select>
      )}
      <div className="filter-field-drag-target">
        <span
          className="filter-field-drag-handle"
          draggable
          aria-hidden="true"
          onDragStart={(event) => startFilterDrag(event, path, "filter")}
          onDragEnd={() => {
            previewPosition.current = undefined;
            onDragPreviewClear();
          }}
        />
        <FieldBuilder
          entity={entity}
          metadata={metadata}
          value={filter.field}
          showPath
          showDataType
          ariaLabel="Filter field"
          onChange={(field) => update({ field })}
          onRemove={() => update({ field: "" })}
        />
      </div>
      <select className="filter-operator-select" value={filter.operation} aria-label="Filter operation" onChange={(event) => update({ operation: Number(event.target.value) })}>
        {operators.map(([value, label]) => <option value={value} key={value}>{label}</option>)}
      </select>
      <input value={filter.value} placeholder="<NULL>" aria-label="Filter value" onChange={(event) => update({ value: event.target.value })} />
      <button
        className="filter-toggle"
        type="button"
        aria-label={filter.enabled ? "Disable filter" : "Enable filter"}
        aria-pressed={filter.enabled}
        title={filter.enabled ? "Disable filter" : "Enable filter"}
        onClick={() => update({ enabled: !filter.enabled })}
      >
        {filter.enabled ? "ON" : "OFF"}
      </button>
      <button className="icon-button filter-action" type="button" aria-label="Add filter after this condition" title="Add filter after this condition" onClick={onAdd}><Icon name="plus" size={13} /></button>
      <button className="icon-button filter-action" type="button" aria-label="Create filter group" title="Create filter group" onClick={onGroup}><Icon name="grid" size={13} /></button>
      <button className="icon-button" type="button" aria-label="Remove filter" onClick={onRemove}><Icon name="trash" size={13} /></button>
    </div>
  );
}

export default function FilterBuilder({ metadata, definition, entity, onDefinitionChange }: FilterBuilderProps) {
  const builderRef = useRef<HTMLDivElement>(null);
  const [filterItemWidth, setFilterItemWidth] = useState<number>();
  const [dragPreview, setDragPreview] = useState<DragPreview>();

  useLayoutEffect(() => {
    const rows = builderRef.current?.querySelectorAll<HTMLElement>(".filter-row");
    if (!rows?.length) {
      setFilterItemWidth(undefined);
      return;
    }

    let maxValueOffset = 0;
    rows.forEach((row) => {
      const valueInput = row.querySelector<HTMLInputElement>("input");
      if (!valueInput) return;
      maxValueOffset = Math.max(maxValueOffset, valueInput.getBoundingClientRect().right - row.getBoundingClientRect().left);
    });
    setFilterItemWidth(maxValueOffset ? Math.max(maxValueOffset - 40, 0) : undefined);
  }, [definition]);

  const moveFilter = (sourcePath: FilterPath, targetPath: FilterPath, position: "inside" | "before" | "after" = "inside") => {
    const source = getNode(definition.filters, sourcePath);
    if (!source || isPathPrefix(sourcePath, targetPath) || isSamePath(sourcePath, targetPath)) return;
    const removed = removeNode(definition.filters, sourcePath);
    if (!removed.node) return;
    if (position === "after" && !targetPath.length) {
      onDefinitionChange({
        ...definition,
        filters: {
          ...removed.group,
          filters: [...removed.group.filters, removed.node],
        },
      });
      return;
    }
    if (position === "after") {
      const adjustedTargetPath = adjustPathAfterRemoval(sourcePath, targetPath);
      const parentPath = adjustedTargetPath.slice(0, -1);
      const targetIndex = adjustedTargetPath[adjustedTargetPath.length - 1];
      if (targetIndex === undefined) return;
      const sourceParentPath = sourcePath.slice(0, -1);
      const sameParent = sourceParentPath.length === parentPath.length
        && sourceParentPath.every((index, pathIndex) => index === parentPath[pathIndex]);
      const sourceIndex = sourcePath[sourcePath.length - 1];
      const insertIndex = sameParent && sourceIndex < targetIndex ? targetIndex : targetIndex + 1;
      onDefinitionChange({
        ...definition,
        filters: updateGroup(removed.group, parentPath, (parent) => ({
          ...parent,
          filters: [
            ...parent.filters.slice(0, insertIndex),
            removed.node!,
            ...parent.filters.slice(insertIndex),
          ],
        })),
      });
      return;
    }
    if (position === "before") {
      if (!targetPath.length) {
        onDefinitionChange({
          ...definition,
          filters: {
            ...removed.group,
            filters: [removed.node, ...removed.group.filters],
          },
        });
        return;
      }
      const parentPath = targetPath.slice(0, -1);
      const targetIndex = targetPath[targetPath.length - 1];
      if (targetIndex === undefined) return;
      const sourceParentPath = sourcePath.slice(0, -1);
      const sameParent = sourceParentPath.length === parentPath.length
        && sourceParentPath.every((index, pathIndex) => index === parentPath[pathIndex]);
      const sourceIndex = sourcePath[sourcePath.length - 1];
      const movingDown = sameParent && sourceIndex < targetIndex;
      const adjustedTargetIndex = movingDown ? targetIndex - 1 : targetIndex;
      const adjustedTargetPath = movingDown
        ? [...targetPath.slice(0, -1), targetIndex - 1]
        : targetPath;
      const target = getNode(removed.group, adjustedTargetPath);
      if (!target || !targetPath.length) return;
      onDefinitionChange({
        ...definition,
        filters: updateGroup(removed.group, parentPath, (parent) => ({
          ...parent,
          filters: [
            ...parent.filters.slice(0, adjustedTargetIndex),
            removed.node!,
            ...parent.filters.slice(adjustedTargetIndex),
          ],
        })),
      });
      return;
    }
    if (!targetPath.length) {
      onDefinitionChange({
        ...definition,
        filters: {
          ...removed.group,
          filters: [removed.node, ...removed.group.filters],
        },
      });
      return;
    }
    const adjustedTargetPath = adjustPathAfterRemoval(sourcePath, targetPath);
    onDefinitionChange({ ...definition, filters: appendToGroup(removed.group, adjustedTargetPath, removed.node) });
  };

  const groupFilter = (path: FilterPath) => {
    const filter = getNode(definition.filters, path);
    if (!filter || isGroup(filter)) return;
    const groupedFilter: QueryFilterGroup = {
      ...createGroup(filter.logicalOperator),
      filters: [{ ...filter, logicalOperator: 0 }],
    };
    onDefinitionChange({ ...definition, filters: replaceNode(definition.filters, path, groupedFilter) });
  };

  const addFilterAfter = (path: FilterPath) => {
    const parentPath = path.slice(0, -1);
    const index = path[path.length - 1];
    const parent = getNode(definition.filters, parentPath);
    if (!parent || !isGroup(parent)) return;
    const filters = [...parent.filters];
    filters.splice(index + 1, 0, createFilter(parent.filters.length ? parent.logicalOperation : 0));
    const updatedParent = { ...parent, filters };
    const replaceParent = (group: QueryFilterGroup, currentPath: FilterPath): QueryFilterGroup => {
      if (!currentPath.length) return updatedParent;
      const [currentIndex, ...rest] = currentPath;
      const child = group.filters[currentIndex];
      if (!child || !isGroup(child)) return group;
      return { ...group, filters: group.filters.map((node, nodeIndex) => nodeIndex === currentIndex ? replaceParent(child, rest) : node) };
    };
    onDefinitionChange({ ...definition, filters: replaceParent(definition.filters, parentPath) });
  };

  const ungroupFilter = (path: FilterPath) => {
    const nested = getNode(definition.filters, path);
    if (!nested || !isGroup(nested) || !path.length) return;
    const parentPath = path.slice(0, -1);
    const index = path[path.length - 1];
    onDefinitionChange({
      ...definition,
      filters: updateGroup(definition.filters, parentPath, (parent) => ({
        ...parent,
        filters: [
          ...parent.filters.slice(0, index),
          ...nested.filters,
          ...parent.filters.slice(index + 1),
        ],
      })),
    });
  };

  return (
    <div
      ref={builderRef}
      className="builder-section filters-section"
      style={filterItemWidth ? { "--filter-item-width": `${filterItemWidth}px` } as CSSProperties : undefined}
    >
      <div className="section-header">
        <span className="section-kicker">FILTERS</span>
      </div>
      <FilterGroupEditor
        group={definition.filters}
        metadata={metadata}
        entity={entity}
        depth={0}
        path={[]}
        onMoveFilter={moveFilter}
        onDragPreview={(path, position) => setDragPreview({ path, position })}
        onDragPreviewClear={() => setDragPreview(undefined)}
        dragPreview={dragPreview}
        onGroupFilter={groupFilter}
        onAddFilter={addFilterAfter}
        onUngroupFilter={ungroupFilter}
        showLogicalOperator={false}
        onChange={(filters) => onDefinitionChange({ ...definition, filters })}
      />
    </div>
  );
}
