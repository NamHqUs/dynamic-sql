import { useEffect, useMemo, useRef, useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import Icon from "../icon/icon";
import { useMessageBox } from "../message-box/message-box";
import { createApiQuery, executeQuery, executeQueryFromText, fetchFunctionDefinitions, fetchQueryMetadata, getQueryRequestString, getSqlStatement, getSqlStatementFromText, MetadataEntity, MetadataField, parseQueryRequestString, QueryDefinition, QueryExpression, QueryFilterGroup, QueryFilterNode } from "./query-design/query-builder-slice";
import PlainText from "./plain-text/plain-text";
import QueryDesign from "./query-design/query-design";
import QueryResult from "./query-result/query-result";
import { addEntityAliasesToMetadata } from "../../utils/query-entity-contexts";
import "./query-builder.css";

type QueryBuilderProps = {
  onNavigationGuardChange: (guard: ((continueNavigation: () => void) => void) | null) => void;
};

let cachedDefinition: QueryDefinition | null = null;

function charIsIndent(character: string | undefined): boolean {
  return character !== undefined && /\s/.test(character);
}

export function resetQueryBuilderCache() {
  cachedDefinition = null;
}

function createDefinition(entity: string, fields: MetadataField[] = []): QueryDefinition {
  return {
    entity,
    entityAliases: [],
    columns: fields.map((field) => ({ field: field.name, alias: null })),
    filters: { logicalOperation: 0, filters: [] },
    sorts: [],
    page: 1,
    pageSize: 50,
  };
}

function loadDefinition(): QueryDefinition {
  if (!cachedDefinition) return createDefinition("");

  return {
    ...cachedDefinition,
    filters: cachedDefinition.filters,
  };
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

function isExpressionAvailable(expression: QueryExpression, entity: MetadataEntity, metadata: MetadataEntity[]): boolean {
  switch (expression.kind) {
    case "field":
      return isAvailableFieldPath(entity, metadata, expression.field);
    case "value":
      return typeof expression.value === "string" || Number.isFinite(expression.value);
    case "binary":
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
  const unchanged = columns.length === definition.columns.length
    && sorts.length === definition.sorts.length
    && JSON.stringify(filters) === JSON.stringify(definition.filters);
  return unchanged ? definition : { ...definition, columns, sorts, filters };
}

export default function QueryBuilder({ onNavigationGuardChange }: QueryBuilderProps) {
  const dispatch = useDispatch();
  const showMessageBox = useMessageBox();
  const { metadata, metadataStatus, metadataError, result, sqlStatement, resultView, queryStatus, queryError, functionDefinitions, functionDefinitionsStatus, functionDefinitionsError, functionNames } = useSelector((state) => state.queryBuilder);
  const [definition, setDefinition] = useState<QueryDefinition>(loadDefinition);
  const definitionRef = useRef(definition);
  const [requestName, setRequestName] = useState("");
  const [activeRequestTab, setActiveRequestTab] = useState<"design" | "structure">("design");
  const [requestStructureDraft, setRequestStructureDraft] = useState("");
  const [requestStructureOriginal, setRequestStructureOriginal] = useState("");
  const [requestStructureDefinition, setRequestStructureDefinition] = useState<string | null>(null);
  const [requestStructureStatus, setRequestStructureStatus] = useState<"idle" | "loading" | "succeeded" | "failed">("idle");
  const [requestStructureError, setRequestStructureError] = useState("");
  const [planTextTextareaHeights, setPlanTextTextareaHeights] = useState({ Entities: 100, Fields: 80, Filters: 80 });
  const [isResolvingPlanChanges, setIsResolvingPlanChanges] = useState(false);
  const planTextDirty = requestStructureStatus === "succeeded" && requestStructureDraft !== requestStructureOriginal;
  const entity = useMemo(() => metadata.find((item) => item.name === definition.entity), [metadata, definition.entity]);
  const contextMetadata = useMemo(
    () => addEntityAliasesToMetadata(metadata, definition.entity, definition.entityAliases),
    [metadata, definition.entity, definition.entityAliases],
  );
  const contextEntity = useMemo(
    () => contextMetadata.find((item) => item.name === definition.entity),
    [contextMetadata, definition.entity],
  );
  const updateDefinition = (nextDefinition: QueryDefinition) => {
    cachedDefinition = nextDefinition;
    definitionRef.current = nextDefinition;
    setDefinition(nextDefinition);
  };
  useEffect(() => {
    dispatch(fetchQueryMetadata());
    dispatch(fetchFunctionDefinitions());
  }, [dispatch]);

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
  }, [metadata, definition.entity, definition.entityAliases]);

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
    }
    setRequestStructureDraft((current) => {
      const lines = current.split(/\r?\n/);
      const fieldLine = lines[index];
      const fieldSeparator = fieldLine?.indexOf(":") ?? -1;
      if (fieldSeparator < 0) return current;
      const nextFieldIndex = lines.findIndex((candidate, lineIndex) =>
        lineIndex > index && candidate.length > 0 && !charIsIndent(candidate[0]) && candidate.indexOf(":") >= 0,
      );
      const fieldEndIndex = nextFieldIndex < 0 ? lines.length : nextFieldIndex;
      if (fieldLine.slice(0, fieldSeparator).trim() === "Entities") {
        lines.splice(
          index,
          fieldEndIndex - index,
          `${fieldLine.slice(0, fieldSeparator + 1)}`,
          ...value.split(/\r?\n/).map((item) => `  ${item}`),
        );
        return lines.join("\n");
      }
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
    const requestLines = requestStructureDraft.split(/\r?\n/);
    const entityLine = requestLines.find((line) => {
      const separator = line.indexOf(":");
      return separator >= 0 && line.slice(0, separator).trim() === "Entity";
    });
    const entitiesIndex = requestLines.findIndex((line) =>
      line.trim().toLowerCase() === "entities:");
    const rootFromEntities = entitiesIndex < 0
      ? undefined
      : requestLines.slice(entitiesIndex + 1)
        .find((line) => line.trim().length > 0 && charIsIndent(line[0]))
        ?.trim();
    const requestEntity = entityLine
      ? entityLine.slice(entityLine.indexOf(":") + 1).trim()
      : rootFromEntities ?? definition.entity;
    const resultAction = await dispatch(parseQueryRequestString({ text: requestStructureDraft, entity: requestEntity }));
    if (parseQueryRequestString.fulfilled.match(resultAction)) {
      const nextDefinition = fromApiQuery(resultAction.payload);
      updateDefinition(nextDefinition);
      setRequestStructureOriginal(requestStructureDraft);
      setRequestStructureDefinition(JSON.stringify(nextDefinition));
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

  const startNewRequest = () => {
    setRequestName("");
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

  const createNewRequest = () => requestPlanTextNavigation(startNewRequest);

  return (
    <>
      <section className="page-heading query-page-heading">
        <div>
          <h2>Query Builder</h2>
        </div>
        <div className="heading-actions">
          <button className="button secondary" onClick={() => void createNewRequest()} disabled={isResolvingPlanChanges}>New Query</button>
        </div>
      </section>

      <section className="query-builder-workspace query-builder-solo">
        <div className={`query-request-editor${activeRequestTab === "structure" ? " query-request-editor-plan-text" : ""}`}>
          <div className="query-request-tab-header">
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
            <span
              className={`query-request-loading${requestStructureStatus === "loading" ? " is-visible" : ""}`}
              role="status"
              aria-hidden={requestStructureStatus !== "loading"}
            >
              Loading request structure…
            </span>
          </div>
          {activeRequestTab === "design" ? (
            <div id="query-request-design-panel" className="query-design-area" role="tabpanel" aria-labelledby="query-request-design-tab">
              <QueryDesign
                metadata={metadata}
                metadataStatus={metadataStatus}
                metadataError={metadataError}
                functionDefinitions={functionDefinitions}
                functionDefinitionsStatus={functionDefinitionsStatus}
                functionDefinitionsError={functionDefinitionsError}
                definition={definition}
                entity={entity}
                requestName={requestName}
                onRequestNameChange={setRequestName}
                onDefinitionChange={updateDefinition}
              />
            </div>
          ) : (
            <PlainText
              id="query-request-structure-panel"
              labelledBy="query-request-structure-tab"
              metadata={contextMetadata}
              status={requestStructureStatus}
              error={requestStructureError}
              text={requestStructureDraft}
              disabled={isResolvingPlanChanges}
              textareaHeights={planTextTextareaHeights}
              onTextareaHeightChange={(field, height) => setPlanTextTextareaHeights((current) => ({
                ...current,
                [field]: height,
              }))}
              onFieldChange={updateRequestStructureField}
              functionNames={functionNames}
              functionNamesStatus={functionDefinitionsStatus}
              functionNamesError={functionDefinitionsError}
            />
          )}
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
