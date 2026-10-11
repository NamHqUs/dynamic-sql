import { createAsyncThunk, createSlice } from "@reduxjs/toolkit";

const apiBaseUrl = import.meta.env.VITE_DYNAMIC_QUERY_API_URL || "";

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
};

async function requestJson(url: string, options?: RequestInit) {
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${url}`, options);
  } catch (error) {
    throw new Error(`Could not reach the query API: ${error instanceof Error ? error.message : "network error"}`);
  }
  if (!response.ok) throw new Error(`Query request failed (${response.status}).`);
  try {
    return await response.json();
  } catch {
    throw new Error("Query API returned invalid JSON.");
  }
}

export const fetchQueryMetadata = createAsyncThunk(
  "queryBuilder/fetchMetadata",
  async (_, { rejectWithValue }) => {
    try {
      const payload = await requestJson("/dynamic-query/metadata");
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
      const { filters, ...query } = definition;
      const payload = await requestJson("/dynamic-query/query", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          ...query,
          filter: createApiFilter(filters),
        }),
      });
      if (!payload || typeof payload.name !== "string" || !Array.isArray(payload.rows) || payload.rows.some((row) => typeof row !== "string")) {
        return rejectWithValue("Query API must return a name and an array of row strings.");
      }
      const responseColumns = typeof payload.columns === "string"
        ? payload.columns.split(",").filter((column: string) => column.trim())
        : [];
      return {
        name: payload.name,
        columns: responseColumns.length ? responseColumns : definition.columns.map((column) => column.alias || column.field),
        rows: payload.rows.map((row: string) => row.split(",")),
      } as QueryResult;
    } catch (error) {
      return rejectWithValue(error instanceof Error ? error.message : "Unable to execute query.");
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
    queryStatus: "idle",
    queryError: "",
  },
  reducers: {
    clearQueryResult(state) {
      state.result = null;
      state.queryError = "";
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
        state.queryStatus = "succeeded";
      })
      .addCase(executeQuery.rejected, (state, action) => {
        state.queryStatus = "failed";
        state.queryError = String(action.payload || action.error.message || "Unable to execute query.");
      });
  },
});

export const { clearQueryResult } = queryBuilderSlice.actions;
export default queryBuilderSlice.reducer;
