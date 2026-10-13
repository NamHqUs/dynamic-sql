import { createAsyncThunk, createSlice } from "@reduxjs/toolkit";
import { apiUrls } from "../../../const/api-urls";

export type MetadataField = {
  name: string;
  dataType: string;
};

export type MetadataRelation = {
  name: string;
  relatedEntity: string;
  type?: number | string;
};

export type MetadataEntity = {
  name: string;
  fields: MetadataField[];
  relations?: MetadataRelation[];
};

export type QueryFilterItem = {
  field: string;
  operation: number;
  value: string;
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

export type QueryDefinition = {
  entity: string;
  columns: { field: string; alias: string | null }[];
  filters: QueryFilterGroup;
  sorts: { field: string; direction: string }[];
  page: number | null;
  pageSize: number | null;
  includeArchive: boolean;
};

export type ApiQueryRequest = Omit<QueryDefinition, "filters" | "sorts"> & {
  name?: string;
  filter: QueryFilterGroup | null;
  sorts: { field: string; direction: number }[];
};

export type StoredQueryRequest = {
  fileName: string;
  query: ApiQueryRequest;
};

function createApiFilter(group: QueryFilterGroup): QueryFilterGroup | null {
  const filters = group.filters.flatMap((filter) => {
    if ("filters" in filter) {
      const nested = createApiFilter(filter);
      return nested ? [nested] : [];
    }
    return filter.enabled && filter.field ? [{
      logicalOperator: filter.logicalOperator,
      field: filter.field,
      operation: filter.operation,
      value: filter.value,
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

function createApiQuery(definition: QueryDefinition) {
  const { filters, sorts, ...query } = definition;
  const noPaging = definition.pageSize !== null && definition.pageSize < 1;
  return {
    ...query,
    page: noPaging ? null : query.page,
    pageSize: noPaging ? null : query.pageSize,
    sorts: sorts.map((sort) => ({ ...sort, direction: sort.direction === "desc" ? 1 : 0 })),
    filter: createApiFilter(filters),
  };
}

function fromApiQuery(query: ApiQueryRequest): QueryDefinition {
  const restoreGroup = (group: QueryFilterGroup): QueryFilterGroup => ({
    ...group,
    filters: group.filters.map((node): QueryFilterNode => {
      if ("filters" in node) return restoreGroup(node);
      return {
        ...node,
        value: String(node.value ?? ""),
        parameterized: node.parameterized ?? false,
        logicalOperator: node.logicalOperator ?? 0,
        enabled: true,
      };
    }),
  });

  return {
    entity: query.entity,
    columns: query.columns,
    filters: query.filter ? restoreGroup(query.filter) : { logicalOperation: 0, filters: [] },
    sorts: query.sorts.map((sort) => ({ ...sort, direction: sort.direction === 1 ? "desc" : "asc" })),
    page: query.page,
    pageSize: query.pageSize,
    includeArchive: query.includeArchive,
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
  async ({ name, definition }: { name: string; definition: QueryDefinition }, { rejectWithValue }) => {
    try {
      return await requestText(apiUrls.queryRequests.toString, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ...createApiQuery(definition), name }),
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
      const payload = await requestJson(apiUrls.queryRequests.parse, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ text, entity }),
      });
      if (!payload || typeof payload.entity !== "string" || !Array.isArray(payload.columns) || !Array.isArray(payload.sorts)) {
        return rejectWithValue("Query request API returned an invalid request.");
      }
      return payload as ApiQueryRequest;
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to apply query request structure.");
    }
  },
);

export const fetchSavedQueryRequests = createAsyncThunk(
  "queryBuilder/fetchSavedRequests",
  async (_, { rejectWithValue }) => {
    try {
      const payload = await requestJson(apiUrls.queryRequests.list);
      if (!Array.isArray(payload) || payload.some((item) => !item || typeof item.fileName !== "string" || !item.query)) {
        return rejectWithValue("Query request API must return saved requests.");
      }
      return payload as StoredQueryRequest[];
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to load saved query requests.");
    }
  },
);

export const saveQueryRequest = createAsyncThunk(
  "queryBuilder/saveRequest",
  async ({ fileName, name, definition }: { fileName?: string; name: string; definition: QueryDefinition }, { rejectWithValue }) => {
    try {
      const url = fileName ? apiUrls.queryRequests.byFileName(fileName) : apiUrls.queryRequests.list;
      const payload = await requestJson(url, {
        method: fileName ? "PUT" : "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ...createApiQuery(definition), name }),
      });
      if (!payload || typeof payload.fileName !== "string" || !payload.query) {
        return rejectWithValue("Query request API returned an invalid saved request.");
      }
      return payload as StoredQueryRequest;
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to save query request.");
    }
  },
);

export const deleteQueryRequest = createAsyncThunk(
  "queryBuilder/deleteRequest",
  async (fileName: string, { rejectWithValue }) => {
    try {
      await requestJson(apiUrls.queryRequests.byFileName(fileName), { method: "DELETE" });
      return fileName;
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to delete query request.");
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
    savedRequests: [] as StoredQueryRequest[],
    savedRequestsStatus: "idle",
    savedRequestsError: "",
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
      .addCase(executeQuery.pending, (state) => {
        state.queryStatus = "loading";
        state.queryError = "";
      })
      .addCase(executeQuery.fulfilled, (state, action) => {
        state.result = action.payload;
        state.resultView = "data";
        state.queryStatus = "succeeded";
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
      .addCase(fetchSavedQueryRequests.pending, (state) => {
        state.savedRequestsStatus = "loading";
        state.savedRequestsError = "";
      })
      .addCase(fetchSavedQueryRequests.fulfilled, (state, action) => {
        state.savedRequests = action.payload;
        state.savedRequestsStatus = "succeeded";
      })
      .addCase(fetchSavedQueryRequests.rejected, (state, action) => {
        state.savedRequestsStatus = "failed";
        state.savedRequestsError = String(action.payload || action.error.message || "Unable to load saved query requests.");
      })
      .addCase(saveQueryRequest.fulfilled, (state, action) => {
        const index = state.savedRequests.findIndex((item) => item.fileName === action.payload.fileName);
        if (index >= 0) state.savedRequests[index] = action.payload;
        else state.savedRequests.unshift(action.payload);
      })
      .addCase(deleteQueryRequest.fulfilled, (state, action) => {
        state.savedRequests = state.savedRequests.filter((item) => item.fileName !== action.payload);
      });
  },
});

export const { clearQueryResult, invalidateQueryMetadata } = queryBuilderSlice.actions;
export { fromApiQuery };
export default queryBuilderSlice.reducer;
