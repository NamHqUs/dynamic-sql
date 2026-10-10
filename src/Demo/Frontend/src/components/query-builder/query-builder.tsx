import { useEffect, useMemo, useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import { executeQuery, fetchQueryMetadata, MetadataEntity, QueryDefinition } from "./query-builder/query-builder-slice";
import Icon from "../icon/icon";
import QueryBuilderForm from "./query-builder/query-builder";
import QueryResult from "./query-result/query-result";
import "./query-builder.css";

function createDefinition(entity: string, metadata: MetadataEntity[]): QueryDefinition {
  const fields = metadata.find((item) => item.name === entity)?.fields || [];
  return {
    entity,
    columns: fields.slice(0, 5).map((field) => ({ field: field.name, alias: null })),
    filters: [],
    sorts: [],
    page: null,
    pageSize: 50,
    includeArchive: false,
  };
}

export default function QueryBuilder() {
  const dispatch = useDispatch();
  const { metadata, metadataStatus, metadataError, result, queryStatus, queryError } = useSelector((state) => state.queryBuilder);
  const [definition, setDefinition] = useState<QueryDefinition>(createDefinition("", []));
  const entity = useMemo(() => metadata.find((item) => item.name === definition.entity), [metadata, definition.entity]);

  useEffect(() => {
    dispatch(fetchQueryMetadata());
  }, [dispatch]);

  useEffect(() => {
    if (!definition.entity && metadata.length) {
      setDefinition(createDefinition(metadata[0].name, metadata));
    }
  }, [metadata, definition.entity]);

  const runQuery = () => {
    if (definition.entity && definition.columns.length) dispatch(executeQuery(definition));
  };

  return (
    <>
      <section className="page-heading query-page-heading">
        <div>
          <div className="eyebrow">WORKSPACE / QUERY BUILDER</div>
          <h1>Build a query</h1>
          <p>Choose metadata fields, define filters, and preview the result from the connected API.</p>
        </div>
        <button className="button primary" onClick={runQuery} disabled={queryStatus === "loading" || !definition.columns.length}>
          <Icon name="play" size={14} /> {queryStatus === "loading" ? "Running…" : "Run query"}
        </button>
      </section>

      <QueryBuilderForm
        metadata={metadata}
        metadataStatus={metadataStatus}
        metadataError={metadataError}
        definition={definition}
        entity={entity}
        onDefinitionChange={setDefinition}
      />

      {queryError && <p className="query-message error" role="alert">{queryError}</p>}
      <QueryResult result={result} />
    </>
  );
}
