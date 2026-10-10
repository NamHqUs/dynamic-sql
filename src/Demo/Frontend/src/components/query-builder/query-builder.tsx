import { useEffect, useMemo, useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import { executeQuery, fetchQueryMetadata, QueryDefinition } from "./query-design/query-builder-slice";
import Icon from "../icon/icon";
import QueryDesign from "./query-design/query-design";
import QueryResult from "./query-result/query-result";
import "./query-builder.css";

let cachedDefinition: QueryDefinition | null = null;

function createDefinition(entity: string): QueryDefinition {
  return {
    entity,
    columns: [],
    filters: [],
    sorts: [],
    page: null,
    pageSize: 50,
    includeArchive: false,
  };
}

function loadDefinition(): QueryDefinition {
  return cachedDefinition ?? createDefinition("");
}

export default function QueryBuilder() {
  const dispatch = useDispatch();
  const { metadata, metadataStatus, metadataError, result, queryStatus, queryError } = useSelector((state) => state.queryBuilder);
  const [definition, setDefinition] = useState<QueryDefinition>(loadDefinition);
  const entity = useMemo(() => metadata.find((item) => item.name === definition.entity), [metadata, definition.entity]);

  const updateDefinition = (nextDefinition: QueryDefinition) => {
    cachedDefinition = nextDefinition;
    setDefinition(nextDefinition);
  };

  useEffect(() => {
    dispatch(fetchQueryMetadata());
  }, [dispatch]);

  useEffect(() => {
    if (metadata.length && !metadata.some((item) => item.name === definition.entity)) {
      updateDefinition(createDefinition(metadata[0].name));
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

      <QueryDesign
        metadata={metadata}
        metadataStatus={metadataStatus}
        metadataError={metadataError}
        definition={definition}
        entity={entity}
        onDefinitionChange={updateDefinition}
      />

      {queryError && <p className="query-message error" role="alert">{queryError}</p>}
      <QueryResult result={result} />
    </>
  );
}
