import { createAsyncThunk, createSlice, type PayloadAction } from "@reduxjs/toolkit";
import { apiUrls } from "../../const/api-urls";

const MAX_TABLE_DATA_ROWS = 10;

async function getJson(url) {
  let response;
  try {
    response = await fetch(url);
  } catch (error) {
    throw new Error(`Could not reach the database API: ${error.message}`);
  }

  if (!response.ok) {
    throw new Error(`Database request failed (${response.status}).`);
  }

  try {
    return await response.json();
  } catch {
    throw new Error("Database API returned invalid JSON.");
  }
}

export const fetchDbTables = createAsyncThunk(
  "database/fetchTables",
  async (_, { rejectWithValue }) => {
    let payload;
    try {
      payload = await getJson(apiUrls.database.tables);
    } catch (error) {
      return rejectWithValue(error.message);
    }
    if (!Array.isArray(payload) || payload.some((table) => typeof table !== "string")) {
      return rejectWithValue("Database tables API must return an array of table names.");
    }
    return payload;
  },
  {
    condition: (_, { getState }) => {
      const { tableStatus } = getState().database;
      return tableStatus !== "loading" && tableStatus !== "succeeded";
    },
  },
);

export const fetchDbSchema = createAsyncThunk(
  "database/fetchSchema",
  async (_, { rejectWithValue }) => {
    let payload;
    try {
      payload = await getJson(apiUrls.database.schema);
    } catch (error) {
      return rejectWithValue(error.message);
    }
    if (!payload || !Array.isArray(payload.tables) || !Array.isArray(payload.relationships)
      || payload.tables.some((table) => typeof table.schema !== "string"
        || typeof table.name !== "string"
        || !Array.isArray(table.columns)
        || table.columns.some((column) => typeof column.name !== "string"
          || typeof column.dataType !== "string"
          || typeof column.isPrimaryKey !== "boolean"))
      || payload.relationships.some((relation) => typeof relation.name !== "string"
        || typeof relation.sourceSchema !== "string"
        || typeof relation.sourceTable !== "string"
        || !Array.isArray(relation.sourceColumns)
        || typeof relation.targetSchema !== "string"
        || typeof relation.targetTable !== "string"
        || !Array.isArray(relation.targetColumns)
        || typeof relation.isUnique !== "boolean")) {
      return rejectWithValue("Database schema API returned invalid table or relationship metadata.");
    }
    return payload;
  },
  {
    condition: (_, { getState }) => {
      const { schemaStatus } = getState().database;
      return schemaStatus !== "loading" && schemaStatus !== "succeeded";
    },
  },
);

function tableDataKey(tableName, schema = "") {
  return schema ? `${schema}.${tableName}` : tableName;
}

export const fetchDbTableData = createAsyncThunk(
  "database/fetchTableData",
  async (request, { rejectWithValue }) => {
    const { tableName, schema = "", limit = MAX_TABLE_DATA_ROWS } = typeof request === "string" ? { tableName: request } : request;
    const rowLimit = Math.min(limit, MAX_TABLE_DATA_ROWS);
    let payload;
    try {
      payload = await getJson(apiUrls.database.tableData(tableName, schema, rowLimit));
    } catch (error) {
      return rejectWithValue(error.message);
    }
    if (!payload || typeof payload.name !== "string" || typeof payload.columns !== "string" || !Array.isArray(payload.rows) || payload.rows.some((row) => typeof row !== "string")) {
      return rejectWithValue("Database table API must return a table name, comma-separated columns, and an array of row strings.");
    }
    return {
      key: tableDataKey(tableName, schema),
      name: payload.name,
      columns: payload.columns ? payload.columns.split(",") : [],
      rows: payload.rows.slice(0, MAX_TABLE_DATA_ROWS).map((row) => row.split(",")),
      limit: rowLimit,
    };
  },
  {
    condition: (request, { getState }) => {
      const { tableName, schema = "", limit = MAX_TABLE_DATA_ROWS, refresh = false } = typeof request === "string" ? { tableName: request } : request;
      const { status, limit: existingLimit } = getState().database.dataByTable[tableDataKey(tableName, schema)] || {};
      const requestedLimit = Math.min(limit, MAX_TABLE_DATA_ROWS);
      return status !== "loading" && (refresh || status !== "succeeded" || existingLimit !== requestedLimit);
    },
  },
);

const databaseSlice = createSlice({
  name: "database",
  initialState: {
    tables: [],
    selectedTable: "",
    selectedSchema: "",
    previewTable: null as { name: string; schema: string } | null,
    viewMode: "diagram",
    tableStatus: "idle",
    tableError: "",
    schemaStatus: "idle",
    schemaError: "",
    schema: { tables: [], relationships: [] },
    dataByTable: {},
    debugUiEnabled: true,
  },
  reducers: {
    setDatabaseDebugUiEnabled(state, action: PayloadAction<boolean>) {
      state.debugUiEnabled = action.payload;
    },
    selectDbTable(state, action) {
      if (typeof action.payload === "string") {
        state.selectedTable = action.payload;
        state.selectedSchema = "";
      } else {
        state.selectedTable = action.payload.name;
        state.selectedSchema = action.payload.schema || "";
      }
    },
    setPreviewDbTable(state, action) {
      state.previewTable = {
        name: action.payload.name,
        schema: action.payload.schema || "",
      };
    },
    setDatabaseViewMode(state, action) {
      state.viewMode = action.payload;
    },
    setColumnFilter(state, action) {
      const { tableName, column, value } = action.payload;
      const tableData = state.dataByTable[tableDataKey(tableName, action.payload.schema || "")];
      if (tableData) tableData.filters[column] = value;
    },
    setTableSort(state, action) {
      const { tableName, column } = action.payload;
      const tableData = state.dataByTable[tableDataKey(tableName, action.payload.schema || "")];
      if (!tableData) return;
      if (tableData.sort?.column === column) {
        tableData.sort.direction = tableData.sort.direction === "asc" ? "desc" : "asc";
      } else {
        tableData.sort = { column, direction: "asc" };
      }
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchDbTables.pending, (state) => {
        state.tableStatus = "loading";
        state.tableError = "";
      })
      .addCase(fetchDbTables.fulfilled, (state, action) => {
        state.tables = action.payload;
        state.selectedTable = action.payload.includes(state.selectedTable) ? state.selectedTable : action.payload[0] || "";
        state.tableStatus = "succeeded";
      })
      .addCase(fetchDbTables.rejected, (state, action) => {
        state.tableStatus = "failed";
        state.tableError = action.payload || action.error.message || "Unable to load database tables.";
      })
      .addCase(fetchDbSchema.pending, (state) => {
        state.schemaStatus = "loading";
        state.schemaError = "";
      })
      .addCase(fetchDbSchema.fulfilled, (state, action) => {
        state.schema = action.payload;
        state.schemaStatus = "succeeded";
      })
      .addCase(fetchDbSchema.rejected, (state, action) => {
        state.schemaStatus = "failed";
        state.schemaError = action.payload || action.error.message || "Unable to load database schema.";
      })
      .addCase(fetchDbTableData.pending, (state, action) => {
        const { tableName, schema = "" } = typeof action.meta.arg === "string" ? { tableName: action.meta.arg } : action.meta.arg;
        const key = tableDataKey(tableName, schema);
        state.dataByTable[key] = {
          ...state.dataByTable[key],
          status: "loading",
          error: "",
        };
      })
      .addCase(fetchDbTableData.fulfilled, (state, action) => {
        const existing = state.dataByTable[action.payload.key];
        state.dataByTable[action.payload.key] = {
          ...existing,
          ...action.payload,
          status: "succeeded",
          error: "",
          filters: existing?.filters || {},
          sort: existing?.sort || null,
        };
      })
      .addCase(fetchDbTableData.rejected, (state, action) => {
        const { tableName, schema = "" } = typeof action.meta.arg === "string" ? { tableName: action.meta.arg } : action.meta.arg;
        const key = tableDataKey(tableName, schema);
        state.dataByTable[key] = {
          ...state.dataByTable[key],
          status: "failed",
          error: action.payload || action.error.message || "Unable to load table data.",
        };
      });
  },
});

export const {
  setDatabaseDebugUiEnabled,
  selectDbTable,
  setPreviewDbTable,
  setDatabaseViewMode,
  setColumnFilter,
  setTableSort,
} = databaseSlice.actions;
export default databaseSlice.reducer;
