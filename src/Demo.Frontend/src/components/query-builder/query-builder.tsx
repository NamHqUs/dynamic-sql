import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import Icon from "../icon/icon";
import PanelSplitter from "../panel-splitter/panel-splitter";
import { useResizablePanel } from "../../utils/use-resizable-panel";
import { useMessageBox } from "../message-box/message-box";
import { deleteQueryRequest, executeQuery, executeQueryFromText, fetchQueryMetadata, fetchSavedQueryRequests, fromApiQuery, getQueryRequestString, getSqlStatement, getSqlStatementFromText, MetadataField, parseQueryRequestString, QueryDefinition, QueryFilterGroup, QueryFilterNode, saveQueryRequest } from "./query-design/query-builder-slice";
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

// Drops columns, sorts and filters that reference fields removed from the entity metadata.
function reconcileDefinition(definition: QueryDefinition, entity: MetadataEntity, metadata: MetadataEntity[]): QueryDefinition {
  const fields = entity.fields;
  const fieldNames = new Set(fields.map((field) => field.name));
  const columns = definition.columns.filter((column) => isProjectionColumnAvailable(column.field, entity, metadata));
  const sorts = definition.sorts.filter((sort) => fieldNames.has(sort.field));
  const filters = pruneFilters(definition.filters, fieldNames);
  const unchanged = columns.length === definition.columns.length
    && sorts.length === definition.sorts.length
    && JSON.stringify(filters) === JSON.stringify(definition.filters);
  return unchanged ? definition : { ...definition, columns, sorts, filters };
}

export default function QueryBuilder({ onNavigationGuardChange }: QueryBuilderProps) {
  const dispatch = useDispatch();
  const showMessageBox = useMessageBox();
  const { metadata, metadataStatus, metadataError, result, sqlStatement, resultView, queryStatus, queryError, savedRequests, savedRequestsStatus, savedRequestsError } = useSelector((state) => state.queryBuilder);
  const [definition, setDefinition] = useState<QueryDefinition>(loadDefinition);
  const [selectedFileName, setSelectedFileName] = useState(cachedSelectedFileName);
  const [requestName, setRequestName] = useState(cachedRequestName);
  const [requestNameInputWidth, setRequestNameInputWidth] = useState(100);
  const [requestMessage, setRequestMessage] = useState("");
  const requestNameInputRef = useRef<HTMLInputElement>(null);
  const requestNameMeasureRef = useRef<HTMLSpanElement>(null);
  const [activeRequestTab, setActiveRequestTab] = useState<"design" | "structure">("design");
  const [requestStructureDraft, setRequestStructureDraft] = useState("");
  const [requestStructureOriginal, setRequestStructureOriginal] = useState("");
  const [requestStructureDefinition, setRequestStructureDefinition] = useState<string | null>(null);
  const [requestStructureStatus, setRequestStructureStatus] = useState<"idle" | "loading" | "succeeded" | "failed">("idle");
  const [requestStructureError, setRequestStructureError] = useState("");
  const [isResolvingPlanChanges, setIsResolvingPlanChanges] = useState(false);
  const panel = useResizablePanel("query-builder");
  const planTextDirty = requestStructureStatus === "succeeded" && requestStructureDraft !== requestStructureOriginal;
  useLayoutEffect(() => {
    const input = requestNameInputRef.current;
    const measure = requestNameMeasureRef.current;
    if (!input || !measure) return;

    const inputStyle = window.getComputedStyle(input);
    measure.style.font = inputStyle.font;
    measure.style.letterSpacing = inputStyle.letterSpacing;
    measure.textContent = input.value || input.placeholder;

    const paddingLeft = Number.parseFloat(inputStyle.paddingLeft);
    const paddingRight = Number.parseFloat(inputStyle.paddingRight);
    setRequestNameInputWidth(
      Math.max(100, measure.getBoundingClientRect().width + paddingLeft + paddingRight + 2),
    );
  }, [requestName]);
  const entity = useMemo(() => metadata.find((item) => item.name === definition.entity), [metadata, definition.entity]);
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
    requestPlanTextNavigation(() => loadSavedRequest(fileName));
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

  const createNewRequest = () => requestPlanTextNavigation(startNewRequest);

  const saveRequest = async () => {
    if (!requestName.trim()) {
      setRequestMessage("A name is required for the query request.");
      return;
    }
    setRequestMessage("");
    const resultAction = await dispatch(saveQueryRequest({
      fileName: selectedFileName || undefined,
      name: requestName.trim(),
      definition,
    }));
    if (saveQueryRequest.fulfilled.match(resultAction)) {
      setSelectedFileName(resultAction.payload.fileName);
      cachedSelectedFileName = resultAction.payload.fileName;
      const savedName = resultAction.payload.query.name || requestName.trim();
      setRequestName(savedName);
      cachedRequestName = savedName;
      setRequestMessage("Query request saved.");
    } else {
      setRequestMessage(String(resultAction.payload || "Unable to save query request."));
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
          <button className="button secondary" onClick={() => void createNewRequest()} disabled={isResolvingPlanChanges}>New request</button>
          <button className="button primary" onClick={saveRequest} disabled={isResolvingPlanChanges}>Save request</button>
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
          {savedRequestsStatus !== "loading" && savedRequests.map((saved) => (
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
          <div className="query-request-header">
            <label><span className="query-request-label-caption">Query name</span><input ref={requestNameInputRef} style={{ width: `${requestNameInputWidth}px` }} value={requestName} onChange={(event) => {
              setRequestName(event.target.value);
              cachedRequestName = event.target.value;
            }} placeholder="e.g. recent-invoices" /><span className="query-request-name-measure" ref={requestNameMeasureRef} aria-hidden="true" /></label>
          </div>
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
          queryLoading={queryStatus === "loading"}
          queryDisabled={isResolvingPlanChanges || (activeRequestTab === "structure"
            ? requestStructureStatus !== "succeeded"
            : !definition.columns.length)}
        />
      </section>
    </>
  );
}
