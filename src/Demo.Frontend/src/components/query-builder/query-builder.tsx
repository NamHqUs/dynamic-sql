import { useEffect, useMemo, useRef, useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import Icon from "../icon/icon";
import PanelSplitter from "../panel-splitter/panel-splitter";
import { useResizablePanel } from "../../utils/use-resizable-panel";
import { useMessageBox } from "../message-box/message-box";
import { createApiQuery, deleteQueryRequest, executeQuery, executeQueryFromText, fetchQueryMetadata, fetchSavedQueryRequests, fromApiQuery, getQueryRequestString, getSqlStatement, getSqlStatementFromText, MetadataField, parseQueryRequestString, QueryDefinition, QueryExpression, QueryFilterGroup, QueryFilterNode, saveQueryRequest } from "./query-design/query-builder-slice";
import QueryDesign from "./query-design/query-design";
import QueryResult from "./query-result/query-result";
import "./query-builder.css";

type QueryBuilderProps = {
  onNavigationGuardChange: (guard: ((continueNavigation: () => void) => void) | null) => void;
};

let cachedDefinition: QueryDefinition | null = null;
let cachedSelectedFileName = "";
let cachedRequestName = "";

export function resetQueryBuilderCache() {
  cachedDefinition = null;
}

function createDefinition(entity: string, fields: MetadataField[] = []): QueryDefinition {
  return {
    entity,
    columns: fields.map((field) => ({ field: field.name, alias: null })),
    groupBy: [],
    filters: { logicalOperation: 0, filters: [] },
    sorts: [],
    page: 1,
    pageSize: 50,
    includeArchive: false,
  };
}

function serializeQueryRequest(name: string, definition: QueryDefinition): string {
  return JSON.stringify({ ...createApiQuery(definition), name });
}

function loadDefinition(): QueryDefinition {
  if (!cachedDefinition) return createDefinition("");

  return {
    ...cachedDefinition,
    groupBy: cachedDefinition.groupBy ?? [],
    filters: cachedDefinition.filters,
  };
}

function pruneFilters(group: QueryFilterGroup, entity: MetadataEntity, metadata: MetadataEntity[]): QueryFilterGroup {
  return {
    ...group,
    filters: group.filters.flatMap((node): QueryFilterNode[] => {
      if ("filters" in node) return [pruneFilters(node, entity, metadata)];
      return isAvailableFieldPath(entity, metadata, node.field) ? [node] : [];
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

function isProjectionColumnAvailable(column: string, entity: MetadataEntity, metadata: MetadataEntity[]): boolean {
  if (entity.fields.some((field) => field.name === column) || column === "Count()") return true;

  const wrappedAggregate = column.match(/^(Sum|Max|Min|Average)\(([^()]*)\)$/);
  if (wrappedAggregate) return isAvailableFieldPath(entity, metadata, wrappedAggregate[2]);

  const pathAggregate = column.match(/^(.*)\.(Count\(\)|Sum|Max|Min|Average)\(([^()]*)\)$/);
  if (!pathAggregate) return false;

  const aggregateOwner = resolveEntityPath(entity, metadata, pathAggregate[1].split("."));
  return Boolean(aggregateOwner && (pathAggregate[2] === "Count()"
    ? !pathAggregate[3]
    : isAvailableFieldPath(aggregateOwner, metadata, pathAggregate[3])));
}

function isExpressionAvailable(expression: QueryExpression, entity: MetadataEntity, metadata: MetadataEntity[]): boolean {
  switch (expression.kind) {
    case "field":
      return isAvailableFieldPath(entity, metadata, expression.field);
    case "value":
      return Number.isFinite(expression.value);
    case "binary":
    case "function":
      return expression.arguments.every((argument) => isExpressionAvailable(argument, entity, metadata));
  }
}

// Drops columns, sorts and filters that reference fields removed from the entity metadata.
function reconcileDefinition(definition: QueryDefinition, entity: MetadataEntity, metadata: MetadataEntity[]): QueryDefinition {
  const columns = definition.columns.filter((column) => column.expression
    ? isExpressionAvailable(column.expression, entity, metadata)
    : isProjectionColumnAvailable(column.field, entity, metadata));
  const sorts = definition.sorts.filter((sort) => isProjectionColumnAvailable(sort.field, entity, metadata)
    || isAvailableFieldPath(entity, metadata, sort.field)
    || columns.some((column) => column.expression && column.alias === sort.field));
  const groupBy = (definition.groupBy ?? []).filter((field) => isAvailableFieldPath(entity, metadata, field));
  const filters = pruneFilters(definition.filters, entity, metadata);
  const unchanged = columns.length === definition.columns.length
    && sorts.length === definition.sorts.length
    && groupBy.length === (definition.groupBy ?? []).length
    && JSON.stringify(filters) === JSON.stringify(definition.filters);
  return unchanged ? definition : { ...definition, columns, sorts, groupBy, filters };
}

export default function QueryBuilder({ onNavigationGuardChange }: QueryBuilderProps) {
  const dispatch = useDispatch();
  const showMessageBox = useMessageBox();
  const { metadata, metadataStatus, metadataError, result, sqlStatement, resultView, queryStatus, queryError, savedRequests, savedRequestsStatus, savedRequestsError } = useSelector((state) => state.queryBuilder);
  const [definition, setDefinition] = useState<QueryDefinition>(loadDefinition);
  const definitionRef = useRef(definition);
  const [selectedFileName, setSelectedFileName] = useState(cachedSelectedFileName);
  const [requestName, setRequestName] = useState(cachedRequestName);
  const [requestMessage, setRequestMessage] = useState("");
  const [savedMessage, setSavedMessage] = useState("");
  const [activeRequestTab, setActiveRequestTab] = useState<"design" | "structure">("design");
  const [requestStructureDraft, setRequestStructureDraft] = useState("");
  const [requestStructureOriginal, setRequestStructureOriginal] = useState("");
  const [requestStructureDefinition, setRequestStructureDefinition] = useState<string | null>(null);
  const [requestStructureStatus, setRequestStructureStatus] = useState<"idle" | "loading" | "succeeded" | "failed">("idle");
  const [requestStructureError, setRequestStructureError] = useState("");
  const [isResolvingPlanChanges, setIsResolvingPlanChanges] = useState(false);
  const panel = useResizablePanel("query-builder");
  const planTextDirty = requestStructureStatus === "succeeded" && requestStructureDraft !== requestStructureOriginal;
  const entity = useMemo(() => metadata.find((item) => item.name === definition.entity), [metadata, definition.entity]);
  const sortedSavedRequests = useMemo(() => [...savedRequests].sort((left, right) => {
    const leftName = left.query.name || left.fileName.replace(/\.json$/i, "");
    const rightName = right.query.name || right.fileName.replace(/\.json$/i, "");
    return leftName.localeCompare(rightName, undefined, { numeric: true, sensitivity: "base" })
      || left.fileName.localeCompare(right.fileName, undefined, { numeric: true, sensitivity: "base" });
  }), [savedRequests]);
  const selectedSavedRequest = savedRequests.find((item) => item.fileName === selectedFileName);
  const defaultNewDefinition = createDefinition(metadata[0]?.name || "", metadata[0]?.fields || []);
  const hasUnsavedChanges = selectedFileName
    ? !selectedSavedRequest
      || serializeQueryRequest(requestName.trim(), definition) !== serializeQueryRequest(
        selectedSavedRequest.query.name || selectedSavedRequest.fileName.replace(/\.json$/i, ""),
        fromApiQuery(selectedSavedRequest.query),
      )
    : serializeQueryRequest(requestName.trim(), definition) !== serializeQueryRequest("", defaultNewDefinition);
  const requestStructureFields = requestStructureDraft.split(/\r?\n/).map((line, index) => {
    const separator = line.indexOf(":");
    return separator < 0
      ? { label: "", value: line, index }
      : {
        label: line.slice(0, separator).trim(),
        value: line.slice(separator + 1).replace(/^ /, ""),
        index,
      };
  }).filter((field) => field.label);

  const updateDefinition = (nextDefinition: QueryDefinition) => {
    cachedDefinition = nextDefinition;
    definitionRef.current = nextDefinition;
    setDefinition(nextDefinition);
  };

  useEffect(() => {
    dispatch(fetchQueryMetadata());
    dispatch(fetchSavedQueryRequests());
  }, [dispatch]);

  useEffect(() => {
    if (metadata.length && !metadata.some((item) => item.name === definition.entity)) {
      updateDefinition(createDefinition(metadata[0].name, metadata[0].fields));
      return;
    }
    const selectedEntity = metadata.find((item) => item.name === definition.entity);
    if (!selectedEntity) return;
    const reconciled = reconcileDefinition(definition, selectedEntity, metadata);
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
    if (activeRequestTab === "structure") {
      dispatch(executeQueryFromText(requestStructureDraft));
      return;
    }
    if (definition.entity && definition.columns.length) {
      dispatch(executeQuery({
        ...definition,
        filters: definition.filters,
      }));
    }
  };

  const changePage = (page: number) => {
    if (activeRequestTab !== "design" || !definition.entity || !definition.columns.length) return;
    const nextDefinition = { ...definition, page };
    updateDefinition(nextDefinition);
    dispatch(executeQuery(nextDefinition));
  };

  const getSql = () => {
    if (activeRequestTab === "structure") {
      dispatch(getSqlStatementFromText(requestStructureDraft));
      return;
    }
    if (definition.entity && definition.columns.length) {
      dispatch(getSqlStatement({
        ...definition,
        filters: definition.filters,
      }));
    }
  };

  const loadRequestStructure = async (nextDefinition = definition, name = requestName) => {
    setRequestStructureStatus("loading");
    setRequestStructureError("");
    const resultAction = await dispatch(getQueryRequestString({
      name,
      definition: nextDefinition,
    }));
    if (getQueryRequestString.fulfilled.match(resultAction)) {
      setRequestStructureDraft(resultAction.payload);
      setRequestStructureOriginal(resultAction.payload);
      setRequestStructureDefinition(JSON.stringify(nextDefinition));
      setRequestStructureStatus("succeeded");
    } else {
      setRequestStructureError(String(resultAction.payload || "Unable to format query request."));
      setRequestStructureStatus("failed");
    }
  };

  const updateRequestStructureField = (index: number, value: string) => {
    setRequestStructureDraft((current) => current.split(/\r?\n/).map((line, lineIndex) => {
      if (lineIndex !== index) return line;
      const separator = line.indexOf(":");
      return `${line.slice(0, separator + 1)} ${value}`;
    }).join("\n"));
  };

  const applyRequestStructure = async () => {
    setIsResolvingPlanChanges(true);
    setRequestStructureError("");
    const resultAction = await dispatch(parseQueryRequestString({ text: requestStructureDraft, entity: definition.entity }));
    if (parseQueryRequestString.fulfilled.match(resultAction)) {
      const nextDefinition = fromApiQuery(resultAction.payload);
      updateDefinition(nextDefinition);
      setRequestStructureOriginal(requestStructureDraft);
      setRequestStructureDefinition(JSON.stringify(nextDefinition));
      setRequestMessage("Query structure applied.");
      setIsResolvingPlanChanges(false);
      return true;
    } else {
      setRequestStructureError(String(resultAction.payload || "Unable to apply query request structure."));
    }
    setIsResolvingPlanChanges(false);
    return false;
  };

  const requestPlanTextNavigation = async (continueNavigation: () => void) => {
    if (isResolvingPlanChanges) return;
    if (!planTextDirty) {
      continueNavigation();
      return;
    }
    const action = await showMessageBox({
      eyebrow: "UNAPPLIED PLAN TEXT",
      title: "Apply your plan text changes?",
      message: "We’ll validate these edits with the backend before continuing. If the plan text is invalid, you’ll stay here so you can fix or discard it.",
      actions: [
        { value: "stay", label: "Stay here", variant: "secondary" },
        { value: "discard", label: "Discard changes", variant: "secondary" },
        { value: "apply", label: "Apply changes", variant: "primary" },
      ],
    });
    if (action === "apply") {
      if (await applyRequestStructure()) continueNavigation();
    } else if (action === "discard") {
      setRequestStructureDraft(requestStructureOriginal);
      setRequestStructureError("");
      continueNavigation();
    }
  };

  const requestQueryChangeNavigation = async (continueNavigation: () => void) => {
    let planTextResolved = false;
    await requestPlanTextNavigation(() => {
      planTextResolved = true;
    });
    if (!planTextResolved) return;

    const currentDefinition = definitionRef.current;
    const hasUnsavedQueryChanges = selectedFileName
      ? !selectedSavedRequest
        || serializeQueryRequest(requestName.trim(), currentDefinition) !== serializeQueryRequest(
          selectedSavedRequest.query.name || selectedSavedRequest.fileName.replace(/\.json$/i, ""),
          fromApiQuery(selectedSavedRequest.query),
        )
      : serializeQueryRequest(requestName.trim(), currentDefinition) !== serializeQueryRequest("", defaultNewDefinition);
    if (!hasUnsavedQueryChanges) {
      continueNavigation();
      return;
    }

    const action = await showMessageBox({
      eyebrow: "UNSAVED QUERY REQUEST",
      title: "Save changes before continuing?",
      message: "Your current query request has unsaved changes.",
      actions: [
        { value: "cancel", label: "Cancel", variant: "secondary" },
        { value: "discard", label: "Discard", variant: "danger" },
        { value: "save", label: "Save", variant: "primary" },
      ],
    });
    if (action === "discard") continueNavigation();
    else if (action === "save" && await saveRequest()) continueNavigation();
  };

  useEffect(() => {
    const guard = (continueNavigation: () => void) => requestPlanTextNavigation(continueNavigation);
    onNavigationGuardChange(guard);
    return () => onNavigationGuardChange(null);
  }, [onNavigationGuardChange, planTextDirty, requestStructureOriginal, isResolvingPlanChanges]);

  useEffect(() => {
    const beforeUnload = (event: BeforeUnloadEvent) => {
      if (!planTextDirty) return;
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", beforeUnload);
    return () => window.removeEventListener("beforeunload", beforeUnload);
  }, [planTextDirty]);

  useEffect(() => {
    if (!savedMessage) return;
    const timeout = window.setTimeout(() => setSavedMessage(""), 3000);
    return () => window.clearTimeout(timeout);
  }, [savedMessage]);

  const loadSavedRequest = (fileName: string) => {
    const saved = savedRequests.find((item) => item.fileName === fileName);
    if (!saved) return;
    setSelectedFileName(saved.fileName);
    cachedSelectedFileName = saved.fileName;
    const nextName = saved.query.name || saved.fileName.replace(/\.json$/i, "");
    setRequestName(nextName);
    cachedRequestName = nextName;
    updateDefinition(fromApiQuery(saved.query));
    setRequestMessage("");
    if (activeRequestTab === "structure") {
      void loadRequestStructure(fromApiQuery(saved.query), nextName);
    }
  };

  const selectSavedRequest = (fileName: string) => {
    if (fileName === selectedFileName) return;
    requestQueryChangeNavigation(() => loadSavedRequest(fileName));
  };

  const startNewRequest = () => {
    setSelectedFileName("");
    setRequestName("");
    cachedSelectedFileName = "";
    cachedRequestName = "";
    setRequestMessage("");
    setRequestStructureError("");
    updateDefinition(createDefinition(metadata[0]?.name || "", metadata[0]?.fields || []));
    setRequestStructureDraft("");
    setRequestStructureOriginal("");
    setRequestStructureDefinition(null);
    setRequestStructureStatus("idle");
  };

  const createNewRequest = () => requestQueryChangeNavigation(startNewRequest);

  const saveRequest = async () => {
    if (!requestName.trim()) {
      setRequestMessage("A name is required for the query request.");
      return false;
    }
    setSavedMessage("");
    setRequestMessage("");
    const resultAction = await dispatch(saveQueryRequest({
      fileName: selectedFileName || undefined,
      name: requestName.trim(),
      definition: definitionRef.current,
    }));
    if (saveQueryRequest.fulfilled.match(resultAction)) {
      setSelectedFileName(resultAction.payload.fileName);
      cachedSelectedFileName = resultAction.payload.fileName;
      const savedName = resultAction.payload.query.name || requestName.trim();
      setRequestName(savedName);
      cachedRequestName = savedName;
      setSavedMessage("Query request saved.");
      return true;
    } else {
      setRequestMessage(String(resultAction.payload || "Unable to save query request."));
      return false;
    }
  };

  const removeRequest = async () => {
    if (!selectedFileName) return;
    const action = await showMessageBox({
      eyebrow: "DELETE QUERY REQUEST",
      title: `Delete "${requestName}"?`,
      message: "This saved query request will be permanently removed.",
      actions: [
        { value: "cancel", label: "Cancel", variant: "secondary" },
        { value: "delete", label: "Delete request", variant: "danger" },
      ],
    });
    if (action !== "delete") return;
    await requestPlanTextNavigation(async () => {
      const resultAction = await dispatch(deleteQueryRequest(selectedFileName));
      if (deleteQueryRequest.fulfilled.match(resultAction)) {
        startNewRequest();
        setRequestMessage("Query request deleted.");
      } else {
        setRequestMessage(String(resultAction.payload || "Unable to delete query request."));
      }
    });
  };

  return (
    <>
      <section className="page-heading query-page-heading">
        <div>
          <div className="eyebrow">WORKSPACE / QUERY BUILDER</div>
          <h1>Build a query</h1>
          <p>Choose metadata fields, define filters, and preview the result from the connected API.</p>
        </div>
        <div className="heading-actions">
          <button className="button danger" type="button" onClick={removeRequest} disabled={!selectedFileName || isResolvingPlanChanges} title="Delete query request" aria-label="Delete query request">
            <Icon name="trash" size={16} />
          </button>
          <button className="button secondary" onClick={() => void createNewRequest()} disabled={isResolvingPlanChanges}>New Query</button>
          <div className="query-save-action">
            <p className="query-save-notification" role="status">{savedMessage}</p>
            <button className="button primary" onClick={saveRequest} disabled={isResolvingPlanChanges || !hasUnsavedChanges} title="Save Query">Save Query</button>
          </div>
        </div>
      </section>

      <section className={`query-builder-workspace split-workspace${panel.collapsed ? " panel-collapsed" : ""}`} style={panel.style}>
        <div className="query-request-workspace">
        <aside className="query-request-list split-panel">
          <div className="metadata-list-head">
            <span className="section-kicker">SAVED QUERY REQUESTS</span>
            <span className="tab-number">{savedRequests.length}</span>
          </div>
          {savedRequestsStatus === "loading" && <p className="query-message">Loading requests…</p>}
          {savedRequestsStatus === "failed" && <p className="query-message error" role="alert">{savedRequestsError}</p>}
          {savedRequestsStatus !== "loading" && sortedSavedRequests.map((saved) => (
            <button className={`query-request-item${saved.fileName === selectedFileName ? " active" : ""}`} key={saved.fileName} onClick={() => void selectSavedRequest(saved.fileName)} disabled={isResolvingPlanChanges}>
              <strong>{saved.query.name || saved.fileName.replace(/\.json$/i, "")}</strong>
              <small>{saved.query.entity} · {saved.query.columns.length} columns</small>
              <Icon name="chevron" size={14} />
            </button>
          ))}
          {savedRequestsStatus !== "loading" && !savedRequests.length && <p className="query-message">No saved requests.</p>}
        </aside>
        <div className="query-request-editor">
          <PanelSplitter panel={panel} label="saved requests panel" />
          {requestMessage && <p className={`query-message${requestMessage.includes("Unable") ? " error" : ""}`} role="status">{requestMessage}</p>}
          <div className="query-request-tabs" role="tablist" aria-label="Query request views">
            <button
              className={`query-request-tab${activeRequestTab === "design" ? " active" : ""}`}
              id="query-request-design-tab"
              type="button"
              role="tab"
              aria-selected={activeRequestTab === "design"}
              aria-controls="query-request-design-panel"
              onClick={() => void requestPlanTextNavigation(() => setActiveRequestTab("design"))}
              disabled={isResolvingPlanChanges}
            >
              <Icon name="grid" size={14} />
              Query design
            </button>
            <button
              className={`query-request-tab${activeRequestTab === "structure" ? " active" : ""}`}
              id="query-request-structure-tab"
              type="button"
              role="tab"
              aria-selected={activeRequestTab === "structure"}
              aria-controls="query-request-structure-panel"
              onClick={() => {
                if (activeRequestTab === "structure" && requestStructureStatus !== "failed") return;
                setActiveRequestTab("structure");
                if (requestStructureStatus !== "succeeded" || requestStructureDefinition !== JSON.stringify(definition)) {
                  void loadRequestStructure();
                }
              }}
              disabled={isResolvingPlanChanges}
            >
              <Icon name="code" size={14} />
              Plan text
            </button>
          </div>
          {activeRequestTab === "design" ? (
            <div id="query-request-design-panel" className="query-design-area" role="tabpanel" aria-labelledby="query-request-design-tab">
              <QueryDesign
                metadata={metadata}
                metadataStatus={metadataStatus}
                metadataError={metadataError}
                definition={definition}
                entity={entity}
                requestName={requestName}
                onRequestNameChange={(name) => {
                  setRequestName(name);
                  cachedRequestName = name;
                }}
                onDefinitionChange={updateDefinition}
              />
            </div>
          ) : (
            <div id="query-request-structure-panel" className="query-request-structure" role="tabpanel" aria-labelledby="query-request-structure-tab">
              {requestStructureStatus === "loading" && <p className="query-message">Loading request structure…</p>}
              {requestStructureStatus === "failed" && <p className="query-message error" role="alert">{requestStructureError}</p>}
              {requestStructureStatus === "succeeded" && (
                <div className="query-request-structure-content">
                  {planTextDirty && (
                    <div className="query-request-structure-actions">
                      <span>Plan text has unapplied changes.</span>
                      <span className="query-plan-text-changed">CHANGED</span>
                    </div>
                  )}
                  {requestStructureError && <p className="query-message error" role="alert">{requestStructureError}</p>}
                  {requestStructureFields.map(({ label, value, index }) => (
                    <div className="query-request-structure-line" key={label}>
                      <span className="query-request-structure-key">{label}:</span>
                      {label === "Entity" ? (
                        <span className="query-request-structure-entity">{value}</span>
                      ) : label === "IncludeArchive" ? (
                        <input
                          className="query-request-structure-checkbox"
                          type="checkbox"
                          aria-label={label}
                          checked={value.trim().toLowerCase() === "true"}
                          onChange={(event) => updateRequestStructureField(index, event.target.checked ? "True" : "False")}
                          disabled={isResolvingPlanChanges}
                        />
                      ) : (
                        <input
                          className="query-request-structure-input"
                          aria-label={label}
                          value={value}
                          onChange={(event) => updateRequestStructureField(index, event.target.value)}
                          disabled={isResolvingPlanChanges}
                        />
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
        </div>
        {queryError && <p className="query-message error query-builder-error" role="alert">{queryError}</p>}
        <QueryResult
          result={result}
          sqlStatement={sqlStatement}
          resultView={resultView}
          onRunQuery={runQuery}
          onGetSql={getSql}
          pageNumber={definition.page ?? 1}
          pageSize={definition.pageSize}
          onPageChange={changePage}
          pageNavigationEnabled={activeRequestTab === "design"}
          queryLoading={queryStatus === "loading"}
          queryDisabled={isResolvingPlanChanges || (activeRequestTab === "structure"
            ? requestStructureStatus !== "succeeded"
            : !definition.columns.length)}
        />
      </section>
    </>
  );
}
