import { useMemo, useState } from "react";
import { MetadataEntity, QueryDefinition } from "./query-builder-slice";
import { FunctionDefinition } from "../../sql-functions/types";
import ProjectionBuilder from "./projection-builder/projection-builder";
import SortBuilder from "./sort-builder/sort-builder";
import FilterBuilder from "./filter-builder/filter-builder";
import { usePlanTextSectionEditor } from "./plan-text-section-editor";
import type { PlanTextSection } from "./plan-text-section-editor";
import { addRelationsToMetadata } from "../../../utils/query-relations";
import "./query-design.css";

type QueryDesignProps = {
  metadata: MetadataEntity[];
  metadataStatus: string;
  metadataError: string;
  functionDefinitions: FunctionDefinition[];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  functionNames: string[];
  definition: QueryDefinition;
  entity?: MetadataEntity;
  requestName: string;
  isBuiltin?: boolean;
  isResolvingPlanChanges: boolean;
  onRequestNameChange: (name: string) => void;
  onDefinitionChange: (definition: QueryDefinition) => void;
  onLoadPlanText: () => Promise<string>;
  onApplyPlanText: (section: PlanTextSection, value: string) => Promise<void>;
};

export default function QueryDesign({
  metadata,
  metadataStatus,
  metadataError,
  functionDefinitions,
  functionDefinitionsStatus,
  functionDefinitionsError,
  functionNames,
  definition,
  entity,
  requestName,
  isBuiltin = false,
  isResolvingPlanChanges,
  onRequestNameChange,
  onDefinitionChange,
  onLoadPlanText,
  onApplyPlanText,
}: QueryDesignProps) {
  const [draggedProjectionField, setDraggedProjectionField] = useState<string | null>(null);
  const contextMetadata = useMemo(
    () => addRelationsToMetadata(metadata, definition.entity, definition.relations),
    [metadata, definition.entity, definition.relations],
  );
  const contextEntity = contextMetadata.find((item) => item.name === definition.entity);
  const filterPlanText = usePlanTextSectionEditor({
    section: "Filter",
    metadata: contextMetadata,
    entityName: definition.entity,
    functionNames,
    disabled: isResolvingPlanChanges,
    readOnly: isBuiltin,
    onLoadPlanText,
    onApplyPlanText,
  });
  const sortPlanText = usePlanTextSectionEditor({
    section: "Sorts",
    metadata: contextMetadata,
    entityName: definition.entity,
    functionNames,
    disabled: isResolvingPlanChanges,
    readOnly: isBuiltin,
    onLoadPlanText,
    onApplyPlanText,
  });

  return (
    <section className={`query-builder-card${isBuiltin ? " is-read-only" : ""}`}>
      <div className="query-name-row" inert={isBuiltin}>
        <label className="query-name-field"><span className="section-kicker">QUERY NAME</span>
          {isBuiltin
            ? <output className="query-static-value">{requestName}</output>
            : <input type="text" value={requestName} onChange={(event) => onRequestNameChange(event.target.value)} placeholder="e.g. recent-invoices" />}
        </label>
      </div>
      <div className="query-builder-toolbar" inert={isBuiltin}>
        <label><span className="section-kicker">ENTITY</span>
          {isBuiltin
            ? <output className="query-static-value">{entity?.name ?? definition.entity}</output>
            : <select value={definition.entity} onChange={(event) => {
              const selectedEntity = metadata.find((item) => item.name === event.target.value);
              onDefinitionChange({
                entity: event.target.value,
                relations: [],
                columns: selectedEntity?.fields.map((field) => ({ field: field.name, alias: null })) ?? [],
                filters: { logicalOperation: 0, filters: [] },
                sorts: [],
                page: 1,
                pageSize: 50,
              });
            }}>
              {!metadata.length && <option value="">Select an entity</option>}
              {metadata.map((item) => <option value={item.name} key={item.name}>{item.name}</option>)}
            </select>}
        </label>
        <label>Page number <input className="small-input" type="number" min="1" step="1" value={definition.page ?? 1} disabled={(definition.page === null && definition.pageSize === null) || (definition.pageSize !== null && definition.pageSize < 1)} onChange={(event) => {
          const value = event.target.value;
          const page = value ? Number.parseInt(value, 10) : null;
          onDefinitionChange({ ...definition, page: page === null ? null : Math.max(1, page) });
        }} /></label>
        <label>Page size <input className="small-input" type="number" step="1" max="1000" value={definition.pageSize ?? ""} onChange={(event) => onDefinitionChange({ ...definition, pageSize: event.target.value ? Number(event.target.value) : null })} /></label>
      </div>

      {metadataStatus === "loading" && <p className="query-message">Loading metadata…</p>}
      {metadataStatus === "failed" && <p className="query-message error" role="alert">{metadataError}</p>}
      {entity && <div className="query-builder-body">
        <ProjectionBuilder
          metadata={contextMetadata}
          functionDefinitions={functionDefinitions}
          functionDefinitionsStatus={functionDefinitionsStatus}
          functionDefinitionsError={functionDefinitionsError}
          definition={definition}
          entity={contextEntity ?? entity}
          isReadOnly={isBuiltin}
          onDefinitionChange={onDefinitionChange}
          onFieldDragStart={setDraggedProjectionField}
        />
        <FilterBuilder
          metadata={contextMetadata}
          definition={definition}
          entity={contextEntity ?? entity}
          isReadOnly={isBuiltin}
          onDefinitionChange={onDefinitionChange}
          planTextControl={filterPlanText.control}
          planTextEditor={filterPlanText.editor}
        />
        <SortBuilder
          metadata={contextMetadata}
          definition={definition}
          entity={contextEntity ?? entity}
          onDefinitionChange={onDefinitionChange}
          draggedProjectionField={draggedProjectionField}
          isReadOnly={isBuiltin}
          planTextControl={sortPlanText.control}
          planTextEditor={sortPlanText.editor}
        />
      </div>}
    </section>
  );
}
