import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import { useSearchParams } from "react-router-dom";
import Icon from "../icon/icon";
import { useMessageBox } from "../message-box/message-box";
import PanelSplitter from "../panel-splitter/panel-splitter";
import { createApiQuery, deleteQueryRequest, executeQuery, fetchFilterOperators, fetchFunctionDefinitions, fetchQueryMetadata, fetchQueryRequests, fromApiQuery, getQueryRequestString, getSqlStatement, IFieldDefinition, MetadataEntity, MetadataField, parseQueryRequestString, QueryDefinition, QueryFilterGroup, QueryFilterNode, saveQueryRequest } from "./query-design/query-builder-slice";
import QueryDesign from "./query-design/query-design";
import QueryResult from "./query-result/query-result";
import SavedQueryList from "./saved-query-list/saved-query-list";
import { addRelationsToMetadata } from "../../utils/query-relations";
import { useResizablePanel } from "../../utils/use-resizable-panel";
import { replacePlanTextSectionValue } from "./query-design/plan-text-section-editor";
import type { PlanTextSection } from "./query-design/plan-text-section-editor";
import type { AppDispatch, RootState } from "../../store/store";
import "./query-builder.css";

type QueryBuilderProps = {
  onBackToReports?: () => void;
};

let cachedDefinition: QueryDefinition | null = null;
const QUERY_DEFINITION_STORAGE_KEY = "dynamic-sql-query-builder-definition";

export function resetQueryBuilderCache() {
  cachedDefinition = null;
  window.sessionStorage.removeItem(QUERY_DEFINITION_STORAGE_KEY);
}

export function setQueryBuilderDefinition(definition: QueryDefinition) {
  cachedDefinition = definition;
  window.sessionStorage.setItem(QUERY_DEFINITION_STORAGE_KEY, JSON.stringify(definition));
}

function createDefinition(entity: string, fields: MetadataField[] = []): QueryDefinition {
  return {
    entity,
    relations: [],
    columns: fields.map((field) => ({ field: field.name, alias: null })),
    filters: { logicalOperation: 0, filters: [] },
    sorts: [],
    filterPlanText: null,
    page: 1,
    pageSize: 50,
  };
}

function countFilters(group: QueryFilterGroup): number {
  return group.filters.reduce(
    (count, filter) => count + ("filters" in filter ? countFilters(filter) : 1),
    0,
  );
}

function loadDefinition(): QueryDefinition {
  if (!cachedDefinition) {
    const savedDefinition = window.sessionStorage.getItem(QUERY_DEFINITION_STORAGE_KEY);
    if (!savedDefinition) return createDefinition("");

    let parsedDefinition: unknown;
    try {
      parsedDefinition = JSON.parse(savedDefinition);
    } catch {
      throw new Error("Saved Query Builder definition is not valid JSON.");
    }
    const normalizedDefinition = normalizeQueryDefinition(parsedDefinition);
    if (!normalizedDefinition) {
      throw new Error("Saved Query Builder definition has an invalid shape.");
    }
    cachedDefinition = normalizedDefinition;
    window.sessionStorage.setItem(QUERY_DEFINITION_STORAGE_KEY, JSON.stringify(normalizedDefinition));
  }

  return {
    ...cachedDefinition,
    filters: cachedDefinition.filters,
  };
}

function normalizeQueryDefinition(value: unknown): QueryDefinition | null {
  if (!isRecord(value) || ("relations" in value && "entityAliases" in value)) return null;

  const savedRelations = value.relations ?? value.entityAliases;
  if (!Array.isArray(savedRelations)) return null;

  const relations = savedRelations.map((savedRelation) => {
    if (!isRecord(savedRelation)) return savedRelation;
    const { alias, ...relation } = savedRelation;
    return {
      ...relation,
      name: typeof relation.name === "string" ? relation.name : alias,
    };
  });
  const normalized = { ...value, relations };
  delete normalized.entityAliases;

  return isQueryDefinition(normalized) ? normalized : null;
}

function isQueryDefinition(value: unknown): value is QueryDefinition {
  if (!isRecord(value)) return false;
  const definition = value as Partial<QueryDefinition>;
  return typeof definition.entity === "string"
    && Array.isArray(definition.relations)
    && definition.relations.every(isRelationDefinition)
    && Array.isArray(definition.columns)
    && definition.columns.every((column) => column !== null
      && typeof column === "object"
      && typeof column.field === "string"
      && (typeof column.alias === "string" || column.alias === null))
    && (definition.projectionRows === undefined
      || Array.isArray(definition.projectionRows)
        && definition.projectionRows.every((row) => Array.isArray(row)
          && row.every((field) => typeof field === "string")))
    && definition.filters !== null
    && typeof definition.filters === "object"
    && Array.isArray(definition.filters.filters)
    && Array.isArray(definition.sorts)
    && (definition.page === null || typeof definition.page === "number")
    && (definition.pageSize === null || typeof definition.pageSize === "number")
    && (definition.filterPlanText === undefined
      || definition.filterPlanText === null
      || typeof definition.filterPlanText === "string");
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function isRelationDefinition(value: unknown): value is QueryDefinition["relations"][number] {
  return isRecord(value)
    && typeof value.name === "string"
    && typeof value.relationPath === "string"
    && Array.isArray(value.relatedKeys)
    && value.relatedKeys.every((key) => isRecord(key)
      && typeof key.fromDbColumn === "string"
      && typeof key.toDbColumn === "string");
}

function pruneFilters(group: QueryFilterGroup, entity: MetadataEntity, metadata: MetadataEntity[]): QueryFilterGroup {
  return {
    ...group,
    filters: group.filters.flatMap((node): QueryFilterNode[] => {
      if ("filters" in node) return [pruneFilters(node, entity, metadata)];
      return isProjectionColumnAvailable(node.field, entity, metadata)
        && (!node.rightField || isProjectionColumnAvailable(node.rightField, entity, metadata))
        ? [node]
        : [];
    }),
  };
}

function resolveEntityPath(entity: MetadataEntity, metadata: MetadataEntity[], path: string[]): MetadataEntity | undefined {
  let current: MetadataEntity | undefined = entity;
  for (const relationName of path) {
    const relation = current?.relations?.find((item) => item.name === relationName);
    current = relation && metadata.find((item) => item.name === relation.relatedEntity);
    if (!current) return undefined;
  }
  return current;
}

function isAvailableFieldPath(entity: MetadataEntity, metadata: MetadataEntity[], fieldPath: string): boolean {
  const segments = fieldPath.split(".");
  const fieldName = segments.pop();
  const owner = resolveEntityPath(entity, metadata, segments);
  return Boolean(fieldName && owner?.fields.some((field) => field.name === fieldName));
}

function isAvailableAggregateField(
  functionName: string,
  fieldName: string,
  entity: MetadataEntity,
  metadata: MetadataEntity[],
): boolean {
  const field = fieldName.trim();
  return (functionName.toLowerCase() === "count" && (!field || field === "*"))
    || isAvailableFieldPath(entity, metadata, field);
}

function isProjectionColumnAvailable(column: string, entity: MetadataEntity, metadata: MetadataEntity[]): boolean {
  if (isAvailableFieldPath(entity, metadata, column)) return true;

  const wrappedAggregate = column.match(/^(Count|Sum|Max|Min|Average)\(([^()]*)\)$/i);
  if (wrappedAggregate) {
    return isAvailableAggregateField(wrappedAggregate[1], wrappedAggregate[2], entity, metadata);
  }

  const pathAggregate = column.match(/^(.*)\.(Count|Sum|Max|Min|Average)\(([^()]*)\)$/i);
  if (!pathAggregate) return false;

  const aggregateOwner = resolveEntityPath(entity, metadata, pathAggregate[1].split("."));
  return Boolean(aggregateOwner
    && isAvailableAggregateField(pathAggregate[2], pathAggregate[3], aggregateOwner, metadata));
}

function isExpressionAvailable(expression: IFieldDefinition, entity: MetadataEntity, metadata: MetadataEntity[]): boolean {
  switch (expression.kind) {
    case "field":
      return isAvailableFieldPath(entity, metadata, expression.field);
    case "value":
      return expression.value === null || typeof expression.value === "string" || typeof expression.value === "boolean"
        || (typeof expression.value === "number" && Number.isFinite(expression.value));
    case "group":
      return expression.arguments.every((argument) => isExpressionAvailable(argument, entity, metadata));
    case "function":
      return expression.arguments.every((argument) =>
        argument.kind === "field" && argument.field === "*"
          ? expression.function.toLowerCase() === "count"
          : isExpressionAvailable(argument, entity, metadata));
  }
}

// Drops columns, sorts and filters that reference fields removed from the entity metadata.
function reconcileDefinition(definition: QueryDefinition, entity: MetadataEntity, metadata: MetadataEntity[]): QueryDefinition {
  const columns = definition.columns.filter((column) => column.expression
    ? isExpressionAvailable(column.expression, entity, metadata)
    : isProjectionColumnAvailable(column.field, entity, metadata));
  const sorts = definition.sorts.filter((sort) => sort.expression
    ? isExpressionAvailable(sort.expression, entity, metadata)
    : isProjectionColumnAvailable(sort.field, entity, metadata)
      || isAvailableFieldPath(entity, metadata, sort.field)
      || columns.some((column) => column.expression && column.alias === sort.field));
  const filters = pruneFilters(definition.filters, entity, metadata);
  const filtersChanged = JSON.stringify(filters) !== JSON.stringify(definition.filters);
  const unchanged = columns.length === definition.columns.length
    && sorts.length === definition.sorts.length
    && !filtersChanged;
  return unchanged ? definition : {
    ...definition,
    columns,
    sorts,
    filters,
    ...(filtersChanged ? { filterPlanText: null } : {}),
  };
}

export default function QueryBuilder({ onBackToReports }: QueryBuilderProps) {
  const dispatch = useDispatch<AppDispatch>();
  const showMessageBox = useMessageBox();
  const [searchParams, setSearchParams] = useSearchParams();
  const savedQueriesPanel = useResizablePanel("query-builder-saved-queries");
  const { metadata, metadataStatus, metadataError, result, sqlStatement, resultView, queryStatus, queryError, functionDefinitions, functionDefinitionsStatus, functionDefinitionsError, functionNames, filterOperators, filterOperatorsStatus, filterOperatorsError, queryRequests, queryRequestsStatus, queryRequestsError, debugUiEnabled } = useSelector((state: RootState) => state.queryBuilder);
  const [definition, setDefinition] = useState<QueryDefinition>(loadDefinition);
  const [requestName, setRequestName] = useState("");
  const [selectedRequestName, setSelectedRequestName] = useState<string | null>(null);
  const [selectedRequestIsBuiltin, setSelectedRequestIsBuiltin] = useState(false);
  const [queryRequestNotice, setQueryRequestNotice] = useState("");
  const [isSavingQuery, setIsSavingQuery] = useState(false);
  const [isApplyingPlanText, setIsApplyingPlanText] = useState(false);
  const planTextCommitRef = useRef<(definition: QueryDefinition) => Promise<QueryDefinition | null>>(async (currentDefinition) => currentDefinition);
  const registerPlanTextCommit = useCallback((commit: ((definition: QueryDefinition) => Promise<QueryDefinition | null>) | null) => {
    planTextCommitRef.current = commit ?? (async (currentDefinition) => currentDefinition);
  }, []);
  const entity = useMemo(() => metadata.find((item) => item.name === definition.entity), [metadata, definition.entity]);
  const contextMetadata = useMemo(
    () => addRelationsToMetadata(metadata, definition.entity, definition.relations),
    [metadata, definition.entity, definition.relations],
  );
  const contextEntity = useMemo(
    () => contextMetadata.find((item) => item.name === definition.entity),
    [contextMetadata, definition.entity],
  );
  const updateDefinition = (nextDefinition: QueryDefinition) => {
    setQueryBuilderDefinition(nextDefinition);
    setDefinition(nextDefinition);
  };
  const updateDefinitionFromDesign = (nextDefinition: QueryDefinition) => {
    updateDefinition(nextDefinition);
  };
  const updateRequestName = (name: string) => {
    setRequestName(name);
  };
  const updateRequestRoute = (name: string | null) => {
    const nextSearchParams = new URLSearchParams(searchParams);
    if (name) nextSearchParams.set("request", name);
    else nextSearchParams.delete("request");
    setSearchParams(nextSearchParams);
  };
  useEffect(() => {
    dispatch(fetchQueryMetadata());
    dispatch(fetchFunctionDefinitions());
    dispatch(fetchFilterOperators());
  }, [dispatch]);

  useEffect(() => {
    if (queryRequestsStatus === "idle") dispatch(fetchQueryRequests());
  }, [dispatch, queryRequestsStatus]);

  useEffect(() => {
    if (metadata.length && !metadata.some((item) => item.name === definition.entity)) {
      updateDefinition(createDefinition(metadata[0].name, metadata[0].fields));
      return;
    }
    const selectedEntity = contextEntity;
    if (!selectedEntity) return;
    const reconciled = reconcileDefinition(definition, selectedEntity, contextMetadata);
    if (!reconciled.columns.length) {
      updateDefinition({
        ...reconciled,
        columns: selectedEntity.fields.map((field) => ({ field: field.name, alias: null })),
      });
    } else if (reconciled !== definition) {
      updateDefinition(reconciled);
    }
  }, [metadata, definition.entity, definition.relations]);

  const runQuery = async () => {
    const queryDefinition = await planTextCommitRef.current(definition);
    if (queryDefinition?.entity && queryDefinition.columns.length) {
      dispatch(executeQuery({
        ...queryDefinition,
        filters: queryDefinition.filters,
      }));
    }
  };

  const changePage = (page: number) => {
    if (!definition.entity || !definition.columns.length) return;
    const nextDefinition = { ...definition, page };
    updateDefinition(nextDefinition);
    dispatch(executeQuery(nextDefinition));
  };

  const getSql = () => {
    if (definition.entity && definition.columns.length) {
      dispatch(getSqlStatement({
        ...definition,
        filters: definition.filters,
      }));
    }
  };

  const loadPlanText = async () => {
    const resultAction = await dispatch(getQueryRequestString({ definition }));
    if (getQueryRequestString.fulfilled.match(resultAction)) return resultAction.payload;
    throw new Error(String(resultAction.payload || "Unable to format query request."));
  };

  const applyPlanText = async (
    section: PlanTextSection,
    value: string,
    currentDefinition: QueryDefinition = definition,
  ): Promise<QueryDefinition> => {
    setIsApplyingPlanText(true);
    try {
      const formatAction = await dispatch(getQueryRequestString({ definition: currentDefinition }));
      if (!getQueryRequestString.fulfilled.match(formatAction)) {
        throw new Error(String(formatAction.payload || "Unable to format query request."));
      }
      const text = replacePlanTextSectionValue(formatAction.payload, section, value);
      const parseAction = await dispatch(parseQueryRequestString({ text, entity: currentDefinition.entity }));
      if (!parseQueryRequestString.fulfilled.match(parseAction)) {
        throw new Error(String(parseAction.payload || "Unable to apply query request."));
      }
      const nextDefinition = {
        ...fromApiQuery(parseAction.payload),
        projectionRows: currentDefinition.projectionRows,
        filterPlanText: section === "Filter" ? value : currentDefinition.filterPlanText,
      };
      updateDefinitionFromDesign(nextDefinition);
      return nextDefinition;
    } finally {
      setIsApplyingPlanText(false);
    }
  };

  const startNewRequest = () => {
    updateRequestRoute(null);
    setSelectedRequestName(null);
    setSelectedRequestIsBuiltin(false);
    setRequestName("");
    setQueryRequestNotice("");
    const nextDefinition = createDefinition(metadata[0]?.name || "", metadata[0]?.fields || []);
    updateDefinition(nextDefinition);
  };

  const selectSavedRequest = (
    savedRequest: (typeof queryRequests)[number],
    updateRoute = true,
  ) => {
    if (debugUiEnabled) console.log("Saved Query Request:", savedRequest);
    if (updateRoute) updateRequestRoute(savedRequest.name);
    updateDefinition({
      ...fromApiQuery(savedRequest.query, savedRequest.projectionRows),
      filterPlanText: savedRequest.filterPlanText ?? null,
    });
    setRequestName(savedRequest.name);
    setSelectedRequestName(savedRequest.name);
    setSelectedRequestIsBuiltin(savedRequest.isBuiltin);
    setQueryRequestNotice("");
  };
  useEffect(() => {
    const requestedName = searchParams.get("request");
    if (!requestedName || selectedRequestName === requestedName) return;
    const savedRequest = queryRequests.find((request) => request.name === requestedName);
    if (savedRequest) selectSavedRequest(savedRequest, false);
  }, [queryRequests, searchParams, selectedRequestName]);

  const persistCurrentRequest = async (currentDefinition: QueryDefinition) => {
    const name = requestName.trim();
    if (!name) {
      setQueryRequestNotice("Enter a query name before saving.");
      return;
    }
    setQueryRequestNotice("");
    setIsSavingQuery(true);
    try {
      const resultAction = await dispatch(saveQueryRequest({ name, definition: currentDefinition }));
      if (saveQueryRequest.fulfilled.match(resultAction)) {
        setSelectedRequestName(resultAction.payload);
        setSelectedRequestIsBuiltin(false);
        setRequestName(resultAction.payload);
        setQueryRequestNotice("Query saved.");
      }
    } finally {
      setIsSavingQuery(false);
    }
  };
  const saveCurrentRequest = () => void persistCurrentRequest(definition);
  const removeCurrentRequest = async () => {
    if (!selectedRequestName || selectedRequestIsBuiltin) return;
    const action = await showMessageBox({
      eyebrow: "DELETE SAVED QUERY",
      title: `Delete “${selectedRequestName}”?`,
      message: "This saved query request will be permanently removed.",
      actions: [
        { value: "cancel", label: "Keep query", variant: "secondary" },
        { value: "delete", label: "Delete query", variant: "danger" },
      ],
    });
    if (action !== "delete") return;
    const resultAction = await dispatch(deleteQueryRequest(selectedRequestName));
    if (deleteQueryRequest.fulfilled.match(resultAction)) {
      updateRequestRoute(null);
      setSelectedRequestName(null);
      setSelectedRequestIsBuiltin(false);
      setRequestName("");
      setQueryRequestNotice("Query deleted. The current design remains available as a draft.");
    }
  };

  return (
    <>
      <section className="page-heading query-page-heading">
        <div className="query-heading-main">
          <span className="query-heading-mark"><Icon name="grid" size={21} /></span>
          <div className="query-heading-copy">
            <span className="query-heading-eyebrow">QUERY WORKSPACE <i /> DESIGN · RUN · REFINE</span>
            <h2>Explore your data</h2>
            <p>Shape a query visually, then inspect its SQL and live results.</p>
            <div className="query-heading-summary" aria-label="Current query summary">
              <span className="query-heading-source"><Icon name="database" size={13} />{entity?.name || "Choose a data source"}</span>
              <span><strong>{definition.columns.length}</strong> fields</span>
              <span><strong>{countFilters(definition.filters)}</strong> filters</span>
              <span><strong>{definition.sorts.length}</strong> sorts</span>
            </div>
          </div>
        </div>
        <div className="heading-actions">
          {onBackToReports && <button className="button secondary" onClick={onBackToReports}><Icon name="chart" size={14} /> Back to reports</button>}
          <button className="button secondary" onClick={startNewRequest}>
            <Icon name="plus" size={14} /> New Query
          </button>
          {!selectedRequestIsBuiltin && <>
            {selectedRequestName && <button className="button secondary" onClick={() => void removeCurrentRequest()}>
              <Icon name="trash" size={14} /> Delete Query
            </button>}
            <button type="button" className="button primary query-save-button" onClick={() => void saveCurrentRequest()} disabled={queryStatus === "loading" || isSavingQuery}>
              <Icon name="check" size={14} /> {isSavingQuery ? "Saving..." : "Save Query"}
            </button>
          </>}
        </div>
      </section>
      {(queryRequestsError || queryRequestNotice) && <p className={`query-request-feedback${queryRequestsError ? " error" : ""}`} role={queryRequestsError ? "alert" : "status"}>
        {queryRequestsError || queryRequestNotice}
      </p>}

      <section className="query-section query-design-section" aria-label="Section 1: Design">
        <div className={`query-design-workspace split-workspace${savedQueriesPanel.collapsed ? " panel-collapsed" : ""}`} style={savedQueriesPanel.style}>
          <SavedQueryList
            requests={queryRequests}
            selectedRequestName={selectedRequestName}
            status={queryRequestsStatus}
            error={queryRequestsError}
            onSelectRequest={selectSavedRequest}
            onRetry={() => {
              void dispatch(fetchQueryRequests());
            }}
          />
          <PanelSplitter panel={savedQueriesPanel} label="saved queries panel" />
          <div className="query-design-area">
            <QueryDesign
              metadata={metadata}
              metadataStatus={metadataStatus}
              metadataError={metadataError}
              functionDefinitions={functionDefinitions}
              functionDefinitionsStatus={functionDefinitionsStatus}
              functionDefinitionsError={functionDefinitionsError}
              functionNames={functionNames}
              filterOperators={filterOperators}
              filterOperatorsStatus={filterOperatorsStatus}
              filterOperatorsError={filterOperatorsError}
              definition={definition}
              entity={entity}
              requestName={requestName}
              isResolvingPlanChanges={isApplyingPlanText}
              onRequestNameChange={updateRequestName}
              onDefinitionChange={updateDefinitionFromDesign}
              onLoadPlanText={loadPlanText}
              onApplyPlanText={applyPlanText}
              onRegisterPlanTextCommit={registerPlanTextCommit}
            />
          </div>
        </div>
        {queryError && <p className="query-message error query-builder-error" role="alert">{queryError}</p>}
      </section>
      <section className="query-section query-result-section" aria-label="Section 2: Query Result">
        <QueryResult
          result={result}
          sqlStatement={sqlStatement}
          resultView={resultView}
          onRunQuery={runQuery}
          onGetSql={getSql}
          pageNumber={definition.page ?? 1}
          pageSize={definition.pageSize}
          onPageChange={changePage}
          pageNavigationEnabled
          queryLoading={queryStatus === "loading"}
          queryDisabled={!definition.columns.length}
        />
      </section>
    </>
  );
}
