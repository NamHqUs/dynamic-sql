import { useEffect, useState } from "react";
import { ApiQueryContextDefinition, FilterOperatorDefinition, MetadataEntity, QueryDefinition } from "./query-builder-slice";
import { FunctionDefinition } from "../../metadata-builder/function-definition/types";
import Icon from "../../icon/icon";
import ProjectionBuilder from "./projection-builder/projection-builder";
import SortBuilder from "./sort-builder/sort-builder";
import FilterBuilder from "./filter-builder/filter-builder";
import { usePlanTextSectionEditor } from "./plan-text-section-editor";
import type { PlanTextSection } from "./plan-text-section-editor";
import "./query-design.css";

type QueryDesignProps = {
  metadata: MetadataEntity[];
  metadataStatus: string;
  metadataError: string;
  functionDefinitions: FunctionDefinition[];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  functionNames: string[];
  filterOperators: FilterOperatorDefinition[];
  filterOperatorsStatus: string;
  filterOperatorsError: string;
  definition: QueryDefinition;
  contextDefinitions: ApiQueryContextDefinition[];
  selectedContextIndex: number | null;
  onSelectContext: (index: number | null) => void;
  entity?: MetadataEntity;
  requestName: string;
  isResolvingPlanChanges: boolean;
  onRequestNameChange: (name: string) => void;
  onDefinitionChange: (definition: QueryDefinition) => void;
  onLoadPlanText: () => Promise<string>;
  onApplyPlanText: (section: PlanTextSection, value: string, definition?: QueryDefinition) => Promise<QueryDefinition>;
  onRegisterPlanTextCommit: (commit: ((definition: QueryDefinition) => Promise<QueryDefinition | null>) | null) => void;
};

export default function QueryDesign({
  metadata,
  metadataStatus,
  metadataError,
  functionDefinitions,
  functionDefinitionsStatus,
  functionDefinitionsError,
  functionNames,
  filterOperators,
  filterOperatorsStatus,
  filterOperatorsError,
  definition,
  contextDefinitions,
  selectedContextIndex,
  onSelectContext,
  entity,
  requestName,
  isResolvingPlanChanges,
  onRequestNameChange,
  onDefinitionChange,
  onLoadPlanText,
  onApplyPlanText,
  onRegisterPlanTextCommit,
}: QueryDesignProps) {
  const [draggedProjectionField, setDraggedProjectionField] = useState<string | null>(null);
  const handleDefinitionChange = (nextDefinition: QueryDefinition) => {
    onDefinitionChange(nextDefinition.filters === definition.filters
      ? nextDefinition
      : { ...nextDefinition, filterPlanText: null });
  };
  const filterPlanText = usePlanTextSectionEditor({
    section: "Filter",
    metadata,
    entityName: definition.entity,
    functionNames,
    disabled: isResolvingPlanChanges,
    onLoadPlanText,
    onApplyPlanText,
    onDraftChange: (value) => handleDefinitionChange({ ...definition, filterPlanText: value }),
    definition,
  });
  const sortPlanText = usePlanTextSectionEditor({
    section: "Sorts",
    metadata,
    entityName: definition.entity,
    functionNames,
    disabled: isResolvingPlanChanges,
    onLoadPlanText,
    onApplyPlanText,
    definition,
  });

  useEffect(() => {
    const commitPlanText = async (currentDefinition: QueryDefinition): Promise<QueryDefinition | null> => {
      let nextDefinition = currentDefinition;
      for (const applyPending of [filterPlanText.applyPending, sortPlanText.applyPending]) {
        const appliedDefinition = await applyPending(nextDefinition, true);
        if (!appliedDefinition) return null;
        nextDefinition = appliedDefinition;
      }
      return nextDefinition;
    };

    onRegisterPlanTextCommit(commitPlanText);
    return () => onRegisterPlanTextCommit(null);
  }, [filterPlanText.applyPending, onRegisterPlanTextCommit, sortPlanText.applyPending]);

  return (
    <section className="query-builder-card">
      <div className="query-name-row">
        <label className="query-name-field"><span className="section-kicker">QUERY NAME</span>
          <input type="text" value={requestName} onChange={(event) => onRequestNameChange(event.target.value)} placeholder="e.g. recent-invoices" />
        </label>
      </div>
      <div className="query-builder-toolbar">
        <label><span className="section-kicker">ENTITY</span>
          <select className="entity-select" value={definition.entity} onChange={(event) => {
            const selectedEntity = metadata.find((item) => item.name === event.target.value);
            onDefinitionChange({
              entity: event.target.value,
              columns: selectedEntity?.fields.map((field) => ({ field: field.name, alias: null })) ?? [],
              contexts: definition.contexts,
              filters: { logicalOperation: 0, filters: [] },
              sorts: [],
              page: 1,
              pageSize: 50,
            });
          }}>
            {!metadata.length && <option value="">Select an entity</option>}
            {definition.contexts?.some((context) => context.name === definition.entity)
              && !metadata.some((item) => item.name === definition.entity)
              && <option value={definition.entity}>{definition.entity}</option>}
            {metadata.map((item) => <option value={item.name} key={item.name}>{item.name}</option>)}
          </select>
        </label>
        {contextDefinitions.length > 0 && <div className="query-context-picker" role="group" aria-label="Query context">
          {contextDefinitions.map((context, index) => <button
            type="button"
            className={`query-context-button${selectedContextIndex === index ? " selected" : ""}`}
            aria-label={`Show ${context.name} context`}
            aria-pressed={selectedContextIndex === index}
            title={`Show ${context.name} context`}
            key={`${context.name}-${index}`}
            onClick={() => onSelectContext(selectedContextIndex === index ? null : index)}
          >
            <Icon name="database" size={14} />
            <span>{context.name}</span>
          </button>)}
        </div>}
      </div>

      {metadataStatus === "loading" && <p className="query-message">Loading metadata…</p>}
      {metadataStatus === "failed" && <p className="query-message error" role="alert">{metadataError}</p>}
      {entity && <div className="query-builder-body">
        <ProjectionBuilder
          metadata={metadata}
          functionDefinitions={functionDefinitions}
          functionDefinitionsStatus={functionDefinitionsStatus}
          functionDefinitionsError={functionDefinitionsError}
          definition={definition}
          entity={entity}
          onDefinitionChange={handleDefinitionChange}
          onFieldDragStart={setDraggedProjectionField}
        />
        <FilterBuilder
          metadata={metadata}
          functionDefinitions={functionDefinitions}
          functionDefinitionsStatus={functionDefinitionsStatus}
          functionDefinitionsError={functionDefinitionsError}
          operators={filterOperators}
          operatorsStatus={filterOperatorsStatus}
          operatorsError={filterOperatorsError}
          definition={definition}
          entity={entity}
          isPlanTextEditing={filterPlanText.isEditing}
          onDefinitionChange={handleDefinitionChange}
          planTextControl={filterPlanText.control}
          planTextEditor={filterPlanText.editor}
        />
        <SortBuilder
          metadata={metadata}
          functionDefinitions={functionDefinitions}
          functionDefinitionsStatus={functionDefinitionsStatus}
          functionDefinitionsError={functionDefinitionsError}
          definition={definition}
          entity={entity}
          onDefinitionChange={handleDefinitionChange}
          draggedProjectionField={draggedProjectionField}
          isPlanTextEditing={sortPlanText.isEditing}
          planTextControl={sortPlanText.control}
          planTextEditor={sortPlanText.editor}
        />
        <div className="builder-section paging-section">
          <div className="section-header">
            <div className="section-heading">
              <div className="projection-title-row">
                <span className="section-kicker">PAGE</span>
                <span className="section-kicker">PAGE SIZE</span>
              </div>
            </div>
          </div>
          <div className="query-builder-toolbar paging-controls">
            <input className="small-input" type="number" min="1" step="1" value={definition.page ?? 1} disabled={(definition.page === null && definition.pageSize === null) || (definition.pageSize !== null && definition.pageSize < 1)} onChange={(event) => {
              const value = event.target.value;
              const page = value ? Number.parseInt(value, 10) : null;
              onDefinitionChange({ ...definition, page: page === null ? null : Math.max(1, page) });
            }} />
            <input className="small-input" type="number" step="1" max="1000" value={definition.pageSize ?? ""} onChange={(event) => onDefinitionChange({ ...definition, pageSize: event.target.value ? Number(event.target.value) : null })} />
          </div>
        </div>
      </div>}
    </section>
  );
}
