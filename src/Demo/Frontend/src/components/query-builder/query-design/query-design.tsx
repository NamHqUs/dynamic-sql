import { MetadataEntity, QueryDefinition } from "./query-builder-slice";
import ProjectionBuilder from "./projection-builder/projection-builder";
import FilterBuilder from "./filter-builder/filter-builder";
import "./query-design.css";

type QueryDesignProps = {
  metadata: MetadataEntity[];
  metadataStatus: string;
  metadataError: string;
  definition: QueryDefinition;
  entity?: MetadataEntity;
  onDefinitionChange: (definition: QueryDefinition) => void;
};

export default function QueryDesign({
  metadata,
  metadataStatus,
  metadataError,
  definition,
  entity,
  onDefinitionChange,
}: QueryDesignProps) {
  return (
    <section className="query-builder-card">
      <div className="query-builder-toolbar">
        <label>Entity
          <select value={definition.entity} onChange={(event) => {
            onDefinitionChange({
              entity: event.target.value,
              columns: [],
              filters: { logicalOperation: 0, filters: [] },
              sorts: [],
              page: null,
              pageSize: 50,
              includeArchive: false,
            });
          }}>
            {!metadata.length && <option value="">Select an entity</option>}
            {metadata.map((item) => <option value={item.name} key={item.name}>{item.name}</option>)}
          </select>
        </label>
        <label className="checkbox-label"><input type="checkbox" checked={definition.includeArchive} onChange={(event) => onDefinitionChange({ ...definition, includeArchive: event.target.checked })} /> Include archived</label>
        <label>Page size <input className="small-input" type="number" min="1" max="1000" value={definition.pageSize || ""} onChange={(event) => onDefinitionChange({ ...definition, pageSize: event.target.value ? Number(event.target.value) : null })} /></label>
      </div>

      {metadataStatus === "loading" && <p className="query-message">Loading metadata…</p>}
      {metadataStatus === "failed" && <p className="query-message error" role="alert">{metadataError}</p>}
      {entity && <div className="query-builder-body">
        <ProjectionBuilder metadata={metadata} definition={definition} entity={entity} onDefinitionChange={onDefinitionChange} />
        <FilterBuilder metadata={metadata} definition={definition} entity={entity} onDefinitionChange={onDefinitionChange} />
      </div>}
    </section>
  );
}
