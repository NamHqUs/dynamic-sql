import { useMemo, useState } from "react";
import { MetadataEntity, QueryDefinition } from "./query-builder-slice";
import { FunctionDefinition } from "../../sql-functions/types";
import EntityContextBuilder from "./entity-context-builder/entity-context-builder";
import ProjectionBuilder from "./projection-builder/projection-builder";
import SortBuilder from "./sort-builder/sort-builder";
import FilterBuilder from "./filter-builder/filter-builder";
import { addEntityAliasesToMetadata } from "../../../utils/query-entity-contexts";
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
  requestNameReadOnly?: boolean;
  onRequestNameChange: (name: string) => void;
  onDefinitionChange: (definition: QueryDefinition) => void;
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
  requestNameReadOnly = false,
  onRequestNameChange,
  onDefinitionChange,
}: QueryDesignProps) {
  const [draggedProjectionField, setDraggedProjectionField] = useState<string | null>(null);
  const contextMetadata = useMemo(
    () => addEntityAliasesToMetadata(metadata, definition.entity, definition.entityAliases),
    [metadata, definition.entity, definition.entityAliases],
  );
  const contextEntity = contextMetadata.find((item) => item.name === definition.entity);

  return (
    <section className="query-builder-card">
      <div className="query-name-row">
        <label className="query-name-field"><span className="section-kicker">QUERY NAME</span>
          <input type="text" value={requestName} readOnly={requestNameReadOnly} onChange={(event) => onRequestNameChange(event.target.value)} placeholder="e.g. recent-invoices" />
        </label>
      </div>
      <div className="query-builder-toolbar">
        <label><span className="section-kicker">ENTITY</span>
          <select value={definition.entity} onChange={(event) => {
            const selectedEntity = metadata.find((item) => item.name === event.target.value);
            onDefinitionChange({
              entity: event.target.value,
              entityAliases: [],
              columns: selectedEntity?.fields.map((field) => ({ field: field.name, alias: null })) ?? [],
              filters: { logicalOperation: 0, filters: [] },
              sorts: [],
              page: 1,
              pageSize: 50,
            });
          }}>
            {!metadata.length && <option value="">Select an entity</option>}
            {metadata.map((item) => <option value={item.name} key={item.name}>{item.name}</option>)}
          </select>
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
        <EntityContextBuilder
          metadata={metadata}
          entity={entity}
          definition={definition}
          onDefinitionChange={onDefinitionChange}
        />
        <ProjectionBuilder
          metadata={contextMetadata}
          functionDefinitions={functionDefinitions}
          functionDefinitionsStatus={functionDefinitionsStatus}
          functionDefinitionsError={functionDefinitionsError}
          definition={definition}
          entity={contextEntity ?? entity}
          onDefinitionChange={onDefinitionChange}
          onFieldDragStart={setDraggedProjectionField}
        />
        <FilterBuilder
          metadata={contextMetadata}
          definition={definition}
          entity={contextEntity ?? entity}
          onDefinitionChange={onDefinitionChange}
        />
        <SortBuilder
          metadata={contextMetadata}
          definition={definition}
          entity={contextEntity ?? entity}
          onDefinitionChange={onDefinitionChange}
          draggedProjectionField={draggedProjectionField}
        />
      </div>}
    </section>
  );
}
