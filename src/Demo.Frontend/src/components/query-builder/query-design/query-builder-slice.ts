import { createAsyncThunk, createSlice } from "@reduxjs/toolkit";
import { apiUrls } from "../../../const/api-urls";
import type { FunctionDefinition } from "../../metadata-builder/function-definition/types";

export type MetadataField = {
  name: string;
  dataType: string;
};

export type FilterOperatorDefinition = {
  id: string;
  name: string;
  symbol: string;
  supportedDataTypes: string[];
  isBuiltin: boolean;
};

export type MetadataRelation = {
  name: string;
  relatedEntity: string;
  type?: number | string;
  relatedKeys?: RelationKey[];
};

export type RelationKey = { fromDbColumn: string; toDbColumn: string };
export type RelationDefinition = {
  name: string;
  relationPath: string;
  relatedKeys: RelationKey[];
};

export type MetadataEntity = {
  name: string;
  fields: MetadataField[];
  relations?: MetadataRelation[];
  canDelete?: boolean;
  rawSql?: string;
};

export type QueryExpression =
  | { kind: "field"; field: string }
  | { kind: "value"; value: string | number }
  | { kind: "binary"; operator: "+" | "-" | "*" | "/"; arguments: [QueryExpression, QueryExpression] }
  | { kind: "function"; function: string; arguments: QueryExpression[] };

export type QueryColumn = {
  field: string;
  alias: string | null;
  expression?: QueryExpression;
};

export type QueryFilterItem = {
  field: string;
  operation: number | string;
  value: string;
  rightField?: string;
  parameterized: boolean;
  logicalOperator: number;
  enabled: boolean;
};

export type QueryFilterNode = QueryFilterItem | QueryFilterGroup;

export type QueryFilterGroup = {
  logicalOperation: number;
  filters: QueryFilterNode[];
  note?: string;
};

type ApiFilterOperand = QueryExpression | { kind: "value"; value: unknown };

type ApiQueryFilterItem = {
  logicalOperator?: number | null;
  left?: ApiFilterOperand | string;
  right?: ApiFilterOperand | string | number | boolean | null;
  operation: number | string;
  parameterized?: boolean;
  field?: ApiFilterOperand | string;
  value?: unknown;
  enabled?: boolean;
};

type ApiQueryFilterNode = ApiQueryFilterItem | ApiQueryFilterGroup;

type ApiQueryFilterGroup = Omit<QueryFilterGroup, "filters"> & {
  filters: ApiQueryFilterNode[];
};

export type QueryDefinition = {
  entity: string;
  relations: RelationDefinition[];
  columns: QueryColumn[];
  projectionRows?: string[][];
  filterPlanText?: string | null;
  filters: QueryFilterGroup;
  sorts: { field: string; direction: string; expression?: QueryExpression }[];
  page: number | null;
  pageSize: number | null;
};

export type ApiQueryRequest = Omit<QueryDefinition, "filters" | "sorts" | "columns" | "projectionRows" | "filterPlanText"> & {
  filter: ApiQueryFilterGroup | null;
  sorts: { field: string | QueryExpression; direction: number }[];
  projection: { field: QueryExpression; alias: string | null }[];
};

export type ReportDto = {
  fileName: string;
  name?: string | null;
  query: ApiQueryRequest;
  requestText?: string | null;
  description?: string | null;
  chartType?: string | null;
  canDelete: boolean;
};

export type SavedQueryRequestDto = {
  name: string;
  query: ApiQueryRequest;
  projectionRows: string[][];
  filterPlanText?: string | null;
  isBuiltin: boolean;
};

type ApiSavedQueryRequestDto = {
  name: string;
  entity: string;
  relations: RelationDefinition[];
  projection: { field: QueryExpression; alias: string | null }[];
  filters: ApiQueryFilterGroup | null;
  sorts: { field: QueryExpression; direction: number }[][] | null;
  page: number | null;
  pageSize: number | null;
  filterPlanText?: string | null;
  isBuiltin: boolean;
};

function createApiFilter(group: QueryFilterGroup): ApiQueryFilterGroup | null {
  const filters = group.filters.flatMap((filter) => {
    if ("filters" in filter) {
      const nested = createApiFilter(filter);
      return nested ? [nested] : [];
    }
    return filter.enabled && filter.field ? [{
      logicalOperator: filter.logicalOperator,
      left: { kind: "field", field: filter.field },
      operation: filter.operation,
      right: filter.rightField
        ? { kind: "field", field: filter.rightField }
        : { kind: "value", value: filter.value },
      parameterized: filter.parameterized,
    }] : [];
  });

  return filters.length ? { logicalOperation: group.logicalOperation, filters } : null;
}

export type QueryResult = {
  name: string;
  columns: string[];
  rows: string[][];
  totalRecords: number;
};

async function requestJson(url: string, options?: RequestInit) {
  let response: Response;
  try {
    response = await fetch(url, options);
  } catch (error) {
    throw new Error(`Could not reach the query API: ${error instanceof Error ? error.message : "network error"}`);
  }
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(detail || `Query request failed (${response.status}).`);
  }
  if (response.status === 204) return null;
  try {
    return await response.json();
  } catch {
    throw new Error("Query API returned invalid JSON.");
  }
}

async function requestText(url: string, options?: RequestInit) {
  let response: Response;
  try {
    response = await fetch(url, options);
  } catch (error) {
    throw new Error(`Could not reach the query API: ${error instanceof Error ? error.message : "network error"}`);
  }
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(detail || `Query request failed (${response.status}).`);
  }
  return response.text();
}

export function createApiQuery(definition: QueryDefinition) {
  const { filters, sorts, columns, ...query } = definition;
  delete query.projectionRows;
  delete query.filterPlanText;
  const noPaging = definition.pageSize !== null && definition.pageSize < 1;
  return {
    ...query,
    projection: columns.map(({ field, alias, expression }) => ({
      field: expression ?? { kind: "field", field },
      alias,
    })),
    page: noPaging ? null : query.page,
    pageSize: noPaging ? null : query.pageSize,
    sorts: sorts.map(({ field, expression, direction }) => ({
      field: expression ?? field,
      direction: direction === "desc" ? 1 : 0,
    })),
    filter: createApiFilter(filters),
  };
}

function expressionToText(expression: QueryExpression): string {
  switch (expression.kind) {
    case "field":
      return expression.field;
    case "value":
      return `<${expression.value}>`;
    case "binary": {
      const [left, right] = expression.arguments;
      const formatOperand = (operand: QueryExpression) => operand.kind === "binary"
        ? `(${expressionToText(operand)})`
        : expressionToText(operand);
      return `${formatOperand(left)} ${expression.operator} ${formatOperand(right)}`;
    }
    case "function":
      return `${expression.function}[${expression.arguments.map(expressionToText).join(", ")}]`;
  }
}

function filterOperandToText(operand: ApiFilterOperand | string): string {
  if (typeof operand === "string") return operand;

  switch (operand.kind) {
    case "field":
      return operand.field;
    case "value":
      return `<${String(operand.value ?? "")}>`;
    case "binary": {
      const [left, right] = operand.arguments;
      const formatOperand = (argument: QueryExpression) => argument.kind === "binary"
        ? `(${filterOperandToText(argument)})`
        : filterOperandToText(argument);
      return `${formatOperand(left)} ${operand.operator} ${formatOperand(right)}`;
    }
    case "function":
      return `${operand.function}(${operand.arguments.map(filterOperandToText).join(", ")})`;
  }
}

function fromApiQuery(query: ApiQueryRequest, projectionRows?: string[][]): QueryDefinition {
  const restoreGroup = (group: ApiQueryFilterGroup): QueryFilterGroup => ({
    ...group,
    filters: group.filters.map((node): QueryFilterNode => {
      if ("filters" in node) return restoreGroup(node);
      const left = node.left ?? node.field;
      const field = left === undefined ? "" : filterOperandToText(left);
      const right = node.right;
      const rightField = right && typeof right === "object" && "kind" in right && right.kind !== "value"
        ? filterOperandToText(right)
        : undefined;
      const value = rightField
        ? ""
        : right && typeof right === "object" && "kind" in right && right.kind === "value"
        ? String(right.value ?? "")
        : "right" in node
          ? String(right ?? "")
          : String(node.value ?? "");
      return {
        field,
        operation: node.operation,
        value,
        ...(rightField ? { rightField } : {}),
        parameterized: node.parameterized ?? false,
        logicalOperator: node.logicalOperator ?? 0,
        enabled: true,
      };
    }),
  });

  return {
    entity: query.entity,
    relations: query.relations ?? [],
    columns: query.projection.map((projection, index) => ({
      field: projection.field.kind === "field"
        ? projection.field.field
        : `__calculated__${projection.alias || index}`,
      alias: projection.alias,
      ...(projection.field.kind === "field" ? {} : { expression: projection.field }),
    })),
    projectionRows: projectionRows?.length
      ? projectionRows.map((row) => [...row])
      : query.projection.length
      ? [query.projection.map((projection, index) => projection.field.kind === "field"
        ? projection.field.field
        : `__calculated__${projection.alias || index}`)]
      : [[]],
    filters: query.filter ? restoreGroup(query.filter) : { logicalOperation: 0, filters: [] },
    sorts: query.sorts.map((sort) => typeof sort.field === "string"
      ? { ...sort, direction: sort.direction === 1 ? "desc" : "asc" }
      : sort.field.kind === "field"
        ? { field: sort.field.field, direction: sort.direction === 1 ? "desc" : "asc" }
        : {
          field: expressionToText(sort.field),
          expression: sort.field,
          direction: sort.direction === 1 ? "desc" : "asc",
        }),
        page: query.page,
        pageSize: query.pageSize,
  };
}

function fromApiSavedRequest(request: ApiSavedQueryRequestDto): SavedQueryRequestDto {
  const fields: ApiSavedQueryRequestDto["projection"] = [];
  const projectionRows: string[][] = [];
  let row: string[] = [];
  request.projection.forEach((projection, index) => {
    if (projection.alias?.toUpperCase() === "NEW_LINE") {
      if (row.length) projectionRows.push(row);
      row = [];
      return;
    }

    fields.push(projection);
    row.push(projection.field.kind === "field"
      ? projection.field.field
      : `__calculated__${projection.alias || index}`);
  });
  if (row.length) projectionRows.push(row);

  return {
    name: request.name,
    query: {
      entity: request.entity,
      relations: request.relations,
      projection: fields,
      filter: request.filters,
      sorts: request.sorts?.flat() ?? [],
      page: request.page,
      pageSize: request.pageSize,
    },
    projectionRows,
    filterPlanText: request.filterPlanText ?? null,
    isBuiltin: request.isBuiltin,
  };
}

function getSavedProjectionRows(definition: QueryDefinition): string[][] {
  const rows = definition.projectionRows?.length
    ? definition.projectionRows
    : definition.columns.length
      ? [definition.columns.map((column) => column.field)]
      : [];
  return rows.filter((row) => row.length).map((row) => [...row]);
}

function toApiSavedRequest(request: { name: string; definition: QueryDefinition }): Omit<ApiSavedQueryRequestDto, "isBuiltin"> & { isBuiltin: false } {
  const apiQuery = createApiQuery(request.definition);
  const rows = getSavedProjectionRows(request.definition);
  const projection = rows.flatMap((row, rowIndex) => [
    ...(rowIndex > 0
      ? [{ field: { kind: "field" as const, field: "" }, alias: "NEW_LINE" }]
      : []),
    ...row.map((field) => {
      const index = request.definition.columns.findIndex((candidate) => candidate.field === field);
      return apiQuery.projection[index] ?? {
        field: { kind: "field" as const, field },
        alias: null,
      };
    }),
  ]);

  return {
    name: request.name,
    entity: apiQuery.entity,
    relations: apiQuery.relations,
    projection,
    filters: apiQuery.filter,
    sorts: apiQuery.sorts.length ? [apiQuery.sorts] : null,
    page: apiQuery.page,
    pageSize: apiQuery.pageSize,
    filterPlanText: request.definition.filterPlanText ?? null,
    isBuiltin: false,
  };
}

export const fetchQueryMetadata = createAsyncThunk(
  "queryBuilder/fetchMetadata",
  async (_, { rejectWithValue }) => {
    try {
      const payload = await requestJson(apiUrls.metadata.list);
      if (!Array.isArray(payload) || payload.some((entity) => !entity
        || typeof entity.name !== "string"
        || !Array.isArray(entity.fields)
        || (entity.relations !== undefined && !Array.isArray(entity.relations)))) {
        return rejectWithValue("Metadata API must return entities with fields.");
      }
      return payload as MetadataEntity[];
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to load query metadata.");
    }
  },
  {
    condition: (_, { getState }) => getState().queryBuilder.metadataStatus !== "loading",
  },
);

export const fetchFunctionDefinitions = createAsyncThunk(
  "queryBuilder/fetchFunctionDefinitions",
  async (_, { rejectWithValue }) => {
    try {
      const payload = await requestJson(apiUrls.metadata.functions);
      if (!Array.isArray(payload) || payload.some((definition) =>
        !definition
        || typeof definition.name !== "string"
        || !definition.name.trim()
        || !Array.isArray(definition.arguments)
        || definition.arguments.some((argument: unknown) => !argument
          || typeof argument !== "object"
          || typeof (argument as { name?: unknown }).name !== "string"
          || typeof (argument as { dataType?: unknown }).dataType !== "string"))) {
        return rejectWithValue("Function metadata API must return function definitions with names and arguments.");
      }
      return payload as FunctionDefinition[];
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to load function metadata.");
    }
  },
  {
    condition: (_, { getState }) => getState().queryBuilder.functionDefinitionsStatus !== "loading",
  },
);

export const fetchFilterOperators = createAsyncThunk(
  "queryBuilder/fetchFilterOperators",
  async (_, { rejectWithValue }) => {
    try {
      const payload = await requestJson(apiUrls.metadata.operators);
      const definitions = Array.isArray(payload)
        ? payload
        : payload && typeof payload === "object" && Array.isArray((payload as { value?: unknown }).value)
          ? (payload as { value: unknown[] }).value
          : null;
      if (!definitions || definitions.some((operator) =>
        !operator
        || typeof operator.id !== "string"
        || typeof operator.name !== "string"
        || typeof operator.symbol !== "string"
        || !Array.isArray(operator.supportedDataTypes))) {
        return rejectWithValue("Operator metadata API must return operators with data types.");
      }
      return definitions as FilterOperatorDefinition[];
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to load filter operators.");
    }
  },
  {
    condition: (_, { getState }) => getState().queryBuilder.filterOperatorsStatus !== "loading",
  },
);

export const executeQuery = createAsyncThunk(
  "queryBuilder/execute",
  async (definition: QueryDefinition, { rejectWithValue }) => {
    try {
      const payload = await requestJson(apiUrls.dynamicQuery.execute, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(createApiQuery(definition)),
      });
      if (!payload || typeof payload.name !== "string" || !Array.isArray(payload.rows) || payload.rows.some((row) => typeof row !== "string") || typeof payload.totalRecords !== "number" || !Number.isFinite(payload.totalRecords) || payload.totalRecords < -1) {
        return rejectWithValue("Query API must return a name, an array of row strings, and a valid total record count.");
      }
      const responseColumns = typeof payload.columns === "string"
        ? payload.columns.split(",").filter((column: string) => column.trim())
        : [];
      return {
        name: payload.name,
        columns: responseColumns.length ? responseColumns : definition.columns.map((column) => column.alias || column.field),
        rows: payload.rows.map((row: string) => row.split(",")),
        totalRecords: payload.totalRecords,
      } as QueryResult;
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to execute query.");
    }
  },
);

export const executeQueryFromText = createAsyncThunk(
  "queryBuilder/executeFromText",
  async (text: string, { rejectWithValue }) => {
    try {
      const payload = await requestJson(apiUrls.dynamicQuery.executeText, {
        method: "POST",
        headers: { "Content-Type": "text/plain" },
        body: text,
      });
      if (!payload || typeof payload.name !== "string" || !Array.isArray(payload.rows) || payload.rows.some((row) => typeof row !== "string") || typeof payload.totalRecords !== "number" || !Number.isFinite(payload.totalRecords) || payload.totalRecords < -1) {
        return rejectWithValue("Query API must return a name, an array of row strings, and a valid total record count.");
      }
      const responseColumns = typeof payload.columns === "string"
        ? payload.columns.split(",").filter((column: string) => column.trim())
        : [];
      return {
        name: payload.name,
        columns: responseColumns,
        rows: payload.rows.map((row: string) => row.split(",")),
        totalRecords: payload.totalRecords,
      } as QueryResult;
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to execute query.");
    }
  },
);

export const getSqlStatement = createAsyncThunk(
  "queryBuilder/getSqlStatement",
  async (definition: QueryDefinition, { rejectWithValue }) => {
    try {
      const payload = await requestText(apiUrls.dynamicQuery.toString, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(createApiQuery(definition)),
      });
      if (typeof payload !== "string") {
        return rejectWithValue("Query API must return a SQL statement string.");
      }
      return payload;
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to generate SQL statement.");
    }
  },
);

export const getSqlStatementFromText = createAsyncThunk(
  "queryBuilder/getSqlStatementFromText",
  async (text: string, { rejectWithValue }) => {
    try {
      return await requestText(apiUrls.dynamicQuery.toStringText, {
        method: "POST",
        headers: { "Content-Type": "text/plain" },
        body: text,
      });
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to generate SQL statement.");
    }
  },
);

export const getQueryRequestString = createAsyncThunk(
  "queryBuilder/getQueryRequestString",
  async ({ definition }: { definition: QueryDefinition }, { rejectWithValue }) => {
    try {
      return await requestText(apiUrls.reports.toString, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(createApiQuery(definition)),
      });
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to format query request.");
    }
  },
);

export const parseQueryRequestString = createAsyncThunk(
  "queryBuilder/parseQueryRequestString",
  async ({ text, entity }: { text: string; entity: string }, { rejectWithValue }) => {
    try {
      const payload = await requestJson(apiUrls.reports.parse, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ text, entity }),
      });
      const projection = payload?.projection ?? payload?.fields;
      if (!payload || typeof payload.entity !== "string" || !Array.isArray(projection) || !Array.isArray(payload.sorts)) {
        return rejectWithValue("Query request API returned an invalid request.");
      }
      return { ...payload, projection } as ApiQueryRequest;
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to apply query request structure.");
    }
  },
);

export const fetchReports = createAsyncThunk(
  "queryBuilder/fetchReports",
  async (_, { rejectWithValue }) => {
    try {
      const payload = await requestJson(apiUrls.reports.list);
      if (!Array.isArray(payload) || payload.some((item) => !item
        || typeof item.fileName !== "string"
        || typeof item.canDelete !== "boolean"
        || !item.query
        || (item.requestText !== undefined && item.requestText !== null && typeof item.requestText !== "string")
        || (item.description !== undefined && item.description !== null && typeof item.description !== "string")
        || (item.chartType !== undefined && item.chartType !== null && typeof item.chartType !== "string"))) {
        return rejectWithValue("Report API must return reports.");
      }
      return payload as ReportDto[];
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to load reports.");
    }
  },
);

export const saveReport = createAsyncThunk(
  "queryBuilder/saveReport",
  async ({ fileName, name, definition, requestText, description, chartType }: {
    fileName?: string;
    name: string;
    definition: QueryDefinition;
    requestText?: string;
    description?: string;
    chartType?: string;
  }, { rejectWithValue }) => {
    try {
      const url = fileName ? apiUrls.reports.byFileName(fileName) : apiUrls.reports.list;
      const payload = await requestJson(url, {
        method: fileName ? "PUT" : "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          ...createApiQuery(definition),
          name,
          ...(requestText !== undefined ? { requestText } : {}),
          ...(description !== undefined ? { description } : {}),
          ...(chartType !== undefined ? { chartType } : {}),
        }),
      });
      if (!payload || typeof payload.fileName !== "string" || !payload.query
        || typeof payload.canDelete !== "boolean") {
        return rejectWithValue("Report API returned an invalid report.");
      }
      return payload as ReportDto;
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to save report.");
    }
  },
);

export const deleteReport = createAsyncThunk(
  "queryBuilder/deleteReport",
  async (fileName: string, { rejectWithValue }) => {
    try {
      await requestJson(apiUrls.reports.byFileName(fileName), { method: "DELETE" });
      return fileName;
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to delete query request.");
    }
  },
);

export const fetchQueryRequests = createAsyncThunk(
  "queryBuilder/fetchQueryRequests",
  async (_, { rejectWithValue }) => {
    try {
      const payload = await requestJson(apiUrls.queryRequests.list);
      if (!Array.isArray(payload) || payload.some((item) => !item
        || typeof item.name !== "string"
        || typeof item.entity !== "string"
        || !Array.isArray(item.relations)
        || typeof item.isBuiltin !== "boolean"
        || !Array.isArray(item.projection)
        || item.projection.some((projection: unknown) => !projection
          || typeof projection !== "object"
          || !("field" in projection)
          || !("alias" in projection))
        || (item.filters !== null && (!item.filters
          || typeof item.filters !== "object"
          || Array.isArray(item.filters)))
        || (item.sorts !== null && (!Array.isArray(item.sorts)
          || item.sorts.some((group: unknown) => !Array.isArray(group))))
        || (item.page !== null && typeof item.page !== "number")
        || (item.pageSize !== null && typeof item.pageSize !== "number"))) {
        return rejectWithValue("Query request API must return saved query requests.");
      }
      return payload.map((item) => fromApiSavedRequest(item as ApiSavedQueryRequestDto));
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to load saved queries.");
    }
  },
);

export const saveQueryRequest = createAsyncThunk(
  "queryBuilder/saveQueryRequest",
  async ({ name, definition }: { name: string; definition: QueryDefinition }, { rejectWithValue }) => {
    try {
      await requestJson(apiUrls.queryRequests.byName(name), {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(toApiSavedRequest({ name, definition })),
      });
      return name;
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to save query.");
    }
  },
);

export const deleteQueryRequest = createAsyncThunk(
  "queryBuilder/deleteQueryRequest",
  async (name: string, { rejectWithValue }) => {
    try {
      await requestJson(apiUrls.queryRequests.byName(name), { method: "DELETE" });
      return name;
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to delete query.");
    }
  },
);

const queryBuilderSlice = createSlice({
  name: "queryBuilder",
  initialState: {
    metadata: [] as MetadataEntity[],
    metadataStatus: "idle",
    metadataError: "",
    result: null as QueryResult | null,
    sqlStatement: null as string | null,
    resultView: "data" as "data" | "sql",
    queryStatus: "idle",
    queryError: "",
    reports: [] as ReportDto[],
    reportsStatus: "idle",
    reportsError: "",
    queryRequests: [] as SavedQueryRequestDto[],
    queryRequestsStatus: "idle",
    queryRequestsError: "",
    functionDefinitions: [] as FunctionDefinition[],
    functionDefinitionsStatus: "idle",
    functionDefinitionsError: "",
    functionNames: [] as string[],
    filterOperators: [] as FilterOperatorDefinition[],
    filterOperatorsStatus: "idle",
    filterOperatorsError: "",
  },
  reducers: {
    clearQueryResult(state) {
      state.result = null;
      state.sqlStatement = null;
      state.resultView = "data";
      state.queryError = "";
    },
    invalidateQueryMetadata(state) {
      state.metadata = [];
      state.metadataStatus = "idle";
      state.metadataError = "";
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchQueryMetadata.pending, (state) => {
        state.metadataStatus = "loading";
        state.metadataError = "";
      })
      .addCase(fetchQueryMetadata.fulfilled, (state, action) => {
        state.metadata = action.payload;
        state.metadataStatus = "succeeded";
      })
      .addCase(fetchQueryMetadata.rejected, (state, action) => {
        state.metadataStatus = "failed";
        state.metadataError = String(action.payload || action.error.message || "Unable to load query metadata.");
      })
      .addCase(fetchFunctionDefinitions.pending, (state) => {
        state.functionDefinitionsStatus = "loading";
        state.functionDefinitionsError = "";
      })
      .addCase(fetchFunctionDefinitions.fulfilled, (state, action) => {
        state.functionDefinitions = action.payload;
        state.functionNames = action.payload.map((definition) => definition.name);
        state.functionDefinitionsStatus = "succeeded";
      })
      .addCase(fetchFunctionDefinitions.rejected, (state, action) => {
        state.functionDefinitionsStatus = "failed";
        state.functionDefinitionsError = String(action.payload || action.error.message || "Unable to load function metadata.");
      })
      .addCase(fetchFilterOperators.pending, (state) => {
        state.filterOperatorsStatus = "loading";
        state.filterOperatorsError = "";
      })
      .addCase(fetchFilterOperators.fulfilled, (state, action) => {
        state.filterOperators = action.payload;
        state.filterOperatorsStatus = "succeeded";
      })
      .addCase(fetchFilterOperators.rejected, (state, action) => {
        state.filterOperatorsStatus = "failed";
        state.filterOperatorsError = String(action.payload || action.error.message || "Unable to load filter operators.");
      })
      .addCase(executeQuery.pending, (state) => {
        state.queryStatus = "loading";
        state.queryError = "";
      })
      .addCase(executeQuery.fulfilled, (state, action) => {
        state.result = action.payload;
        state.resultView = "data";
        state.queryStatus = "succeeded";
        state.queryError = "";
      })
      .addCase(executeQuery.rejected, (state, action) => {
        state.queryStatus = "failed";
        state.queryError = String(action.payload || action.error.message || "Unable to execute query.");
      })
      .addCase(executeQueryFromText.pending, (state) => {
        state.queryStatus = "loading";
        state.queryError = "";
      })
      .addCase(executeQueryFromText.fulfilled, (state, action) => {
        state.result = action.payload;
        state.resultView = "data";
        state.queryStatus = "succeeded";
        state.queryError = "";
      })
      .addCase(executeQueryFromText.rejected, (state, action) => {
        state.queryStatus = "failed";
        state.queryError = String(action.payload || action.error.message || "Unable to execute query.");
      });
    builder
      .addCase(getSqlStatement.pending, (state) => {
        state.queryStatus = "loading";
        state.queryError = "";
      })
      .addCase(getSqlStatement.fulfilled, (state, action) => {
        state.sqlStatement = action.payload;
        state.resultView = "sql";
        state.queryStatus = "succeeded";
      })
      .addCase(getSqlStatement.rejected, (state, action) => {
        state.queryStatus = "failed";
        state.queryError = String(action.payload || action.error.message || "Unable to generate SQL statement.");
      })
      .addCase(getSqlStatementFromText.pending, (state) => {
        state.queryStatus = "loading";
        state.queryError = "";
      })
      .addCase(getSqlStatementFromText.fulfilled, (state, action) => {
        state.sqlStatement = action.payload;
        state.resultView = "sql";
        state.queryStatus = "succeeded";
      })
      .addCase(getSqlStatementFromText.rejected, (state, action) => {
        state.queryStatus = "failed";
        state.queryError = String(action.payload || action.error.message || "Unable to generate SQL statement.");
      })
      .addCase(fetchReports.pending, (state) => {
        state.reportsStatus = "loading";
        state.reportsError = "";
      })
      .addCase(fetchReports.fulfilled, (state, action) => {
        state.reports = action.payload;
        state.reportsStatus = "succeeded";
      })
      .addCase(fetchReports.rejected, (state, action) => {
        state.reportsStatus = "failed";
        state.reportsError = String(action.payload || action.error.message || "Unable to load reports.");
      })
      .addCase(saveReport.fulfilled, (state, action) => {
        const savedReport = {
          ...action.payload,
          requestText: action.payload.requestText ?? action.meta.arg.requestText ?? null,
        };
        const index = state.reports.findIndex((item) => item.fileName === savedReport.fileName);
        if (index >= 0) state.reports[index] = savedReport;
        else state.reports.unshift(savedReport);
      })
      .addCase(deleteReport.fulfilled, (state, action) => {
        state.reports = state.reports.filter((item) => item.fileName !== action.payload);
      })
      .addCase(fetchQueryRequests.pending, (state) => {
        state.queryRequestsStatus = "loading";
        state.queryRequestsError = "";
      })
      .addCase(fetchQueryRequests.fulfilled, (state, action) => {
        state.queryRequests = action.payload;
        state.queryRequestsStatus = "succeeded";
      })
      .addCase(fetchQueryRequests.rejected, (state, action) => {
        state.queryRequestsStatus = "failed";
        state.queryRequestsError = String(action.payload || action.error.message || "Unable to load saved queries.");
      })
      .addCase(saveQueryRequest.pending, (state) => {
        state.queryRequestsError = "";
      })
      .addCase(saveQueryRequest.fulfilled, (state, action) => {
        state.queryRequestsError = "";
        state.queryRequestsStatus = "succeeded";
        state.queryRequests = state.queryRequests.filter((item) =>
          item.isBuiltin || item.name.toLowerCase() !== action.payload.toLowerCase());
        const definition = action.meta.arg.definition;
        state.queryRequests.unshift({
          name: action.payload,
          query: createApiQuery(definition),
          projectionRows: getSavedProjectionRows(definition),
          isBuiltin: false,
        });
      })
      .addCase(saveQueryRequest.rejected, (state, action) => {
        state.queryRequestsError = String(action.payload || action.error.message || "Unable to save query.");
      })
      .addCase(deleteQueryRequest.fulfilled, (state, action) => {
        state.queryRequests = state.queryRequests.filter((item) =>
          item.name.toLowerCase() !== action.payload.toLowerCase());
      })
      .addCase(deleteQueryRequest.rejected, (state, action) => {
        state.queryRequestsError = String(action.payload || action.error.message || "Unable to delete query.");
      });
  },
});

export const { clearQueryResult, invalidateQueryMetadata } = queryBuilderSlice.actions;
export { fromApiQuery };
export default queryBuilderSlice.reducer;
