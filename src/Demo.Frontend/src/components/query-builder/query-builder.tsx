import { useEffect, useMemo, useRef, useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import Icon from "../icon/icon";
import PanelSplitter from "../panel-splitter/panel-splitter";
import { useResizablePanel } from "../../utils/use-resizable-panel";
import { useMessageBox } from "../message-box/message-box";
import { createApiQuery, deleteQueryRequest, executeQuery, executeQueryFromText, fetchFunctionNames, fetchQueryMetadata, fetchSavedQueryRequests, fromApiQuery, getQueryRequestString, getSqlStatement, getSqlStatementFromText, MetadataField, parseQueryRequestString, QueryDefinition, QueryExpression, QueryFilterGroup, QueryFilterNode, saveQueryRequest } from "./query-design/query-builder-slice";
import PlainText from "./plain-text/plain-text";
import QueryDesign from "./query-design/query-design";
import QueryResult from "./query-result/query-result";
import SavedQueries from "./saved-queries/saved-queries";
import "./query-builder.css";

type QueryBuilderProps = {
  onNavigationGuardChange: (guard: ((continueNavigation: () => void) => void) | null) => void;
};

let cachedDefinition: QueryDefinition | null = null;
let cachedSelectedFileName = "";
let cachedRequestName = "";
let cachedSavedRequestBaseline: { fileName: string; serialized: string } | null = null;

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
  const sortObjectKeys = (value: unknown): unknown => {
    if (Array.isArray(value)) return value.map(sortObjectKeys);
    if (value && typeof value === "object") {
      return Object.fromEntries(
        Object.entries(value).sort(([left], [right]) => left.localeCompare(right))
          .map(([key, item]) => [key, sortObjectKeys(item)]),
      );
    }
    return value;
  };
  return JSON.stringify(sortObjectKeys({ ...createApiQuery(definition), name }));
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
      return isAvailableFieldPath(entity, metadata, node.field)
        && (!node.rightField || isAvailableFieldPath(entity, metadata, node.rightField))
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

function isProjectionColumnAvailable(column: string, entity: MetadataEntity, metadata: MetadataEntity[]): boolean {
  if (isAvailableFieldPath(entity, metadata, column) || column === "Count()") return true;

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
      return typeof expression.value === "string" || Number.isFinite(expression.value);
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
  const sorts = definition.sorts.filter((sort) => sort.expression
    ? isExpressionAvailable(sort.expression, entity, metadata)
    : isProjectionColumnAvailable(sort.field, entity, metadata)
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
  const { metadata, metadataStatus, metadataError, result, sqlStatement, resultView, queryStatus, queryError, savedRequests, savedRequestsStatus, savedRequestsError, functionNames, functionNamesStatus, functionNamesError } = useSelector((state) => state.queryBuilder);
  const [definition, setDefinition] = useState<QueryDefinition>(loadDefinition);
  const definitionRef = useRef(definition);
  const [selectedFileName, setSelectedFileName] = useState(cachedSelectedFileName);
  const [requestName, setRequestName] = useState(cachedRequestName);
  const savedRequestBaselineRef = useRef(cachedSavedRequestBaseline);
  const [requestMessage, setRequestMessage] = useState("");
  const [savedMessage, setSavedMessage] = useState("");
  const [savedMessageIsError, setSavedMessageIsError] = useState(false);
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
  const selectedSavedRequest = savedRequests.find((item) => item.fileName === selectedFileName);
  const defaultNewDefinition = createDefinition(metadata[0]?.name || "", metadata[0]?.fields || []);
  const hasUnsavedQueryChanges = (currentDefinition: QueryDefinition) => {
    const currentRequest = serializeQueryRequest(requestName.trim(), currentDefinition);
    if (!selectedFileName) {
      return currentRequest !== serializeQueryRequest("", defaultNewDefinition);
    }
    const baseline = savedRequestBaselineRef.current?.fileName === selectedFileName
      ? savedRequestBaselineRef.current.serialized
      : selectedSavedRequest
        ? serializeQueryRequest(
          selectedSavedRequest.query.name || selectedSavedRequest.fileName.replace(/\.json$/i, ""),
          fromApiQuery(selectedSavedRequest.query),
        )
        : null;
    return baseline === null || currentRequest !== baseline;
  };
  const hasUnsavedChanges = hasUnsavedQueryChanges(definition);
  const updateDefinition = (nextDefinition: QueryDefinition) => {
    cachedDefinition = nextDefinition;
    definitionRef.current = nextDefinition;
    setDefinition(nextDefinition);
  };
  const updateSavedRequestBaseline = (fileName: string, name: string, nextDefinition: QueryDefinition) => {
    const baseline = { fileName, serialized: serializeQueryRequest(name.trim(), nextDefinition) };
    savedRequestBaselineRef.current = baseline;
    cachedSavedRequestBaseline = baseline;
  };

  useEffect(() => {
    dispatch(fetchQueryMetadata());
    dispatch(fetchFunctionNames());
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
    const line = requestStructureDraft.split(/\r?\n/)[index];
    const separator = line?.indexOf(":") ?? -1;
    if (separator >= 0 && line.slice(0, separator).trim() === "Name") {
      setRequestName(value);
      cachedRequestName = value;
    }
    setRequestStructureDraft((current) => {
      const lines = current.split(/\r?\n/);
      const fieldLine = lines[index];
      const fieldSeparator = fieldLine?.indexOf(":") ?? -1;
      if (fieldSeparator < 0) return current;
      const nextFieldIndex = lines.findIndex((candidate, lineIndex) =>
        lineIndex > index && candidate.indexOf(":") >= 0,
      );
      const fieldEndIndex = nextFieldIndex < 0 ? lines.length : nextFieldIndex;
      lines.splice(
        index,
        fieldEndIndex - index,
        `${fieldLine.slice(0, fieldSeparator + 1)} ${value}`,
      );
      return lines.join("\n");
    });
  };

  const applyRequestStructure = async (keepResolving = false) => {
    setIsResolvingPlanChanges(true);
    setRequestStructureError("");
    const entityLine = requestStructureDraft.split(/\r?\n/).find((line) => {
      const separator = line.indexOf(":");
      return separator >= 0 && line.slice(0, separator).trim() === "Entity";
    });
    const requestEntity = entityLine
      ? entityLine.slice(entityLine.indexOf(":") + 1).trim()
      : definition.entity;
    const resultAction = await dispatch(parseQueryRequestString({ text: requestStructureDraft, entity: requestEntity }));
    if (parseQueryRequestString.fulfilled.match(resultAction)) {
      const nextDefinition = fromApiQuery(resultAction.payload);
      updateDefinition(nextDefinition);
      setRequestStructureOriginal(requestStructureDraft);
      setRequestStructureDefinition(JSON.stringify(nextDefinition));
      setSavedMessageIsError(false);
      setSavedMessage("Query structure applied.");
      if (!keepResolving) setIsResolvingPlanChanges(false);
      return true;
    } else {
      setRequestStructureError(String(resultAction.payload || "Unable to apply query request structure."));
    }
    if (!keepResolving) setIsResolvingPlanChanges(false);
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
    if (!planTextDirty && !hasUnsavedQueryChanges(definitionRef.current)) {
      continueNavigation();
      return;
    }

    const action = await showMessageBox({
      eyebrow: "UNSAVED QUERY REQUEST",
      title: "Save changes before continuing?",
      message: planTextDirty
        ? "Your current query request or plan text has unsaved changes."
        : "Your current query request has unsaved changes.",
      actions: [
        { value: "cancel", label: "Cancel", variant: "secondary" },
        { value: "discard", label: "Discard", variant: "danger" },
        { value: "save", label: "Save", variant: "primary" },
      ],
    });
    if (action === "discard") {
      if (planTextDirty) {
        setRequestStructureDraft(requestStructureOriginal);
        setRequestStructureError("");
      }
      continueNavigation();
    }
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
    const timeout = window.setTimeout(() => setSavedMessage(""), 5000);
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
    const nextDefinition = fromApiQuery(saved.query);
    updateDefinition(nextDefinition);
    updateSavedRequestBaseline(saved.fileName, nextName, nextDefinition);
    setRequestMessage("");
    if (typeof saved.requestText === "string") {
      setRequestStructureDraft(saved.requestText);
      setRequestStructureOriginal(saved.requestText);
      setRequestStructureDefinition(JSON.stringify(nextDefinition));
      setRequestStructureStatus("succeeded");
      setRequestStructureError("");
    } else {
      setRequestStructureDefinition(null);
      setRequestStructureStatus("idle");
      setRequestStructureError("");
      if (activeRequestTab === "structure") {
        void loadRequestStructure(nextDefinition, nextName);
      }
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
    savedRequestBaselineRef.current = null;
    cachedSavedRequestBaseline = null;
    setRequestMessage("");
    setRequestStructureError("");
    const nextDefinition = createDefinition(metadata[0]?.name || "", metadata[0]?.fields || []);
    updateDefinition(nextDefinition);
    setRequestStructureDraft("");
    setRequestStructureOriginal("");
    setRequestStructureDefinition(null);
    setRequestStructureStatus("idle");
    if (activeRequestTab === "structure") {
      void loadRequestStructure(nextDefinition, "");
    }
  };

  const createNewRequest = () => requestQueryChangeNavigation(startNewRequest);

  const saveRequest = async () => {
    if (!requestName.trim()) {
      setSavedMessageIsError(true);
      setSavedMessage("A name is required for the query request.");
      return false;
    }
    setSavedMessage("");
    setSavedMessageIsError(false);
    setRequestMessage("");
    let requestText: string | undefined;
    if (planTextDirty && !(await applyRequestStructure(true))) {
      setIsResolvingPlanChanges(false);
      return false;
    }
    if (requestStructureStatus === "succeeded" && (
      activeRequestTab === "structure"
      || requestStructureDefinition === JSON.stringify(definitionRef.current)
    )) {
      requestText = requestStructureDraft;
    }
    if (requestText !== undefined) {
      requestText = requestText.replace(/^Name:.*$/m, `Name: ${requestName.trim()}`);
    }
    const resultAction = await dispatch(saveQueryRequest({
      fileName: selectedFileName || undefined,
      name: requestName.trim(),
      definition: definitionRef.current,
      requestText,
    }));
    setIsResolvingPlanChanges(false);
    if (saveQueryRequest.fulfilled.match(resultAction)) {
      setSelectedFileName(resultAction.payload.fileName);
      cachedSelectedFileName = resultAction.payload.fileName;
      const savedName = resultAction.payload.query.name || requestName.trim();
      setRequestName(savedName);
      cachedRequestName = savedName;
      updateSavedRequestBaseline(resultAction.payload.fileName, savedName, definitionRef.current);
      if (requestText !== undefined) {
        setRequestStructureDraft(requestText);
        setRequestStructureOriginal(requestText);
        setRequestStructureDefinition(JSON.stringify(definitionRef.current));
        setRequestStructureStatus("succeeded");
      }
      setSavedMessageIsError(false);
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
          <h2>Query Builder</h2>
        </div>
        <div className="heading-actions">
          <button className="button danger" type="button" onClick={removeRequest} disabled={!selectedFileName || isResolvingPlanChanges} title="Delete query request" aria-label="Delete query request">
            <Icon name="trash" size={16} />
          </button>
          <button className="button secondary" onClick={() => void createNewRequest()} disabled={isResolvingPlanChanges}>New Query</button>
          <div className="query-save-action">
            {savedMessage && (
              <p
                className={`query-save-notification${savedMessageIsError ? " error" : ""}`}
                role={savedMessageIsError ? "alert" : "status"}
              >
                {savedMessage}
              </p>
            )}
            <button className="button primary" onClick={saveRequest} disabled={isResolvingPlanChanges || (!hasUnsavedChanges && !planTextDirty)} title="Save Query">Save Query</button>
          </div>
        </div>
      </section>

      <section className={`query-builder-workspace split-workspace${panel.collapsed ? " panel-collapsed" : ""}`} style={panel.style}>
        <div className="query-request-workspace">
          <SavedQueries
            requests={savedRequests}
            status={savedRequestsStatus}
            error={savedRequestsError}
            selectedFileName={selectedFileName}
            disabled={isResolvingPlanChanges}
            onSelectRequest={(fileName) => void selectSavedRequest(fileName)}
          />
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
            <PlainText
              id="query-request-structure-panel"
              labelledBy="query-request-structure-tab"
              metadata={metadata}
              status={requestStructureStatus}
              error={requestStructureError}
              text={requestStructureDraft}
              disabled={isResolvingPlanChanges}
              onFieldChange={updateRequestStructureField}
              functionNames={functionNames}
              functionNamesStatus={functionNamesStatus}
              functionNamesError={functionNamesError}
            />
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
