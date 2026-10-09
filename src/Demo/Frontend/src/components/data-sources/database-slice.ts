import { createAsyncThunk, createSlice } from "@reduxjs/toolkit";

const apiBaseUrl = import.meta.env.VITE_DATABASE_API_URL || "";

async function getJson(url) {
  let response;
  try {
    response = await fetch(`${apiBaseUrl}${url}`);
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
      payload = await getJson("/db/tables");
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

export const fetchDbTableData = createAsyncThunk(
  "database/fetchTableData",
  async (tableName, { rejectWithValue }) => {
    let payload;
    try {
      payload = await getJson(`/db/data/${encodeURIComponent(tableName)}`);
    } catch (error) {
      return rejectWithValue(error.message);
    }
    if (!payload || typeof payload.name !== "string" || typeof payload.columns !== "string" || !Array.isArray(payload.rows) || payload.rows.some((row) => typeof row !== "string")) {
      return rejectWithValue("Database table API must return a table name, comma-separated columns, and an array of row strings.");
    }
    return {
      name: payload.name,
      columns: payload.columns ? payload.columns.split(",") : [],
      rows: payload.rows.map((row) => row.split(",")),
    };
  },
  {
    condition: (tableName, { getState }) => {
      const status = getState().database.dataByTable[tableName]?.status;
      return status !== "loading" && status !== "succeeded";
    },
  },
);

const databaseSlice = createSlice({
  name: "database",
  initialState: {
    tables: [],
    selectedTable: "",
    tableStatus: "idle",
    tableError: "",
    dataByTable: {},
  },
  reducers: {
    selectDbTable(state, action) {
      state.selectedTable = action.payload;
    },
    setColumnFilter(state, action) {
      const { tableName, column, value } = action.payload;
      const tableData = state.dataByTable[tableName];
      if (tableData) tableData.filters[column] = value;
    },
    setTableSort(state, action) {
      const { tableName, column } = action.payload;
      const tableData = state.dataByTable[tableName];
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
      .addCase(fetchDbTableData.pending, (state, action) => {
        state.dataByTable[action.meta.arg] = {
          ...state.dataByTable[action.meta.arg],
          status: "loading",
          error: "",
        };
      })
      .addCase(fetchDbTableData.fulfilled, (state, action) => {
        const existing = state.dataByTable[action.meta.arg];
        state.dataByTable[action.meta.arg] = {
          ...existing,
          ...action.payload,
          status: "succeeded",
          error: "",
          filters: existing?.filters || {},
          sort: existing?.sort || null,
        };
      })
      .addCase(fetchDbTableData.rejected, (state, action) => {
        state.dataByTable[action.meta.arg] = {
          ...state.dataByTable[action.meta.arg],
          status: "failed",
          error: action.payload || action.error.message || "Unable to load table data.",
        };
      });
  },
});

export const { selectDbTable, setColumnFilter, setTableSort } = databaseSlice.actions;
export default databaseSlice.reducer;
