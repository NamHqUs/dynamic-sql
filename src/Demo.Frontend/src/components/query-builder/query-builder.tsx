import { useEffect, useMemo, useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import { executeQuery, fetchQueryMetadata, getSqlStatement, MetadataField, QueryDefinition, QueryFilterGroup, QueryFilterNode } from "./query-design/query-builder-slice";
import QueryDesign from "./query-design/query-design";
import QueryResult from "./query-result/query-result";
import "./query-builder.css";

let cachedDefinition: QueryDefinition | null = null;

export function resetQueryBuilderCache() {
  cachedDefinition = null;
}

function createDefinition(entity: string, fields: MetadataField[] = []): QueryDefinition {
  return {
    entity,
    columns: fields.map((field) => ({ field: field.name, alias: null })),
    filters: { logicalOperation: 0, filters: [] },
    sorts: [],
    page: null,
    pageSize: 50,
    includeArchive: false,
  };
}

function loadDefinition(): QueryDefinition {
  if (!cachedDefinition) return createDefinition("");

  return {
    ...cachedDefinition,
    filters: cachedDefinition.filters,
  };
}

function pruneFilters(group: QueryFilterGroup, fieldNames: Set<string>): QueryFilterGroup {
  return {
    ...group,
    filters: group.filters.flatMap((node): QueryFilterNode[] => {
      if ("filters" in node) return [pruneFilters(node, fieldNames)];
      return fieldNames.has(node.field) ? [node] : [];
    }),
  };
}

// Drops columns, sorts and filters that reference fields removed from the entity metadata.
function reconcileDefinition(definition: QueryDefinition, fields: MetadataField[]): QueryDefinition {
  const fieldNames = new Set(fields.map((field) => field.name));
  const columns = definition.columns.filter((column) => fieldNames.has(column.field));
  const sorts = definition.sorts.filter((sort) => fieldNames.has(sort.field));
  const filters = pruneFilters(definition.filters, fieldNames);
  const unchanged = columns.length === definition.columns.length
    && sorts.length === definition.sorts.length
    && JSON.stringify(filters) === JSON.stringify(definition.filters);
  return unchanged ? definition : { ...definition, columns, sorts, filters };
}

export default function QueryBuilder() {
  const dispatch = useDispatch();
  const { metadata, metadataStatus, metadataError, result, sqlStatement, resultView, queryStatus, queryError } = useSelector((state) => state.queryBuilder);
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
      updateDefinition(createDefinition(metadata[0].name, metadata[0].fields));
      return;
    }
    const selectedEntity = metadata.find((item) => item.name === definition.entity);
    if (!selectedEntity) return;
    const reconciled = reconcileDefinition(definition, selectedEntity.fields);
    if (!reconciled.columns.length) {
      updateDefinition({
        ...reconciled,
        columns: selectedEntity.fields.map((field) => ({ field: field.name, alias: null })),
      });
    } else if (reconciled !== definition) {
      updateDefinition(reconciled);
    }
  }, [metadata, definition.entity]);

  const runQuery = () => {
    if (definition.entity && definition.columns.length) {
      dispatch(executeQuery({
        ...definition,
        filters: definition.filters,
      }));
    }
  };

  const getSql = () => {
    if (definition.entity && definition.columns.length) {
      dispatch(getSqlStatement({
        ...definition,
        filters: definition.filters,
      }));
    }
  };

  return (
    <>
      <section className="page-heading query-page-heading">
        <div>
          <div className="eyebrow">WORKSPACE / QUERY BUILDER</div>
          <h1>Build a query</h1>
          <p>Choose metadata fields, define filters, and preview the result from the connected API.</p>
        </div>
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
      <QueryResult
        result={result}
        sqlStatement={sqlStatement}
        resultView={resultView}
        onRunQuery={runQuery}
        onGetSql={getSql}
        queryLoading={queryStatus === "loading"}
        queryDisabled={!definition.columns.length}
      />
    </>
  );
}
