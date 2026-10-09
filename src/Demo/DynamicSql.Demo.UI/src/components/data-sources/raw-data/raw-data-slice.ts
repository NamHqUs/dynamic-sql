import { createSlice } from "@reduxjs/toolkit";

const primaryEntityId = "customer_revenue";

const rawDataSlice = createSlice({
  name: "rawData",
  initialState: {
    entities: [],
    previewEntityIds: [],
    collapsedEntityIds: [],
    searchTerm: "",
    error: "",
    preservePrimaryEntityName: false,
  },
  reducers: {
    syncPrimaryEntity(state, action) {
      const { rows, sourceName } = action.payload;
      const existing = state.entities.find((entity) => entity.id === primaryEntityId);
      const sourceIsSecondaryEntity = state.entities.some((entity) => entity.id !== primaryEntityId && entity.name === sourceName);
      if (existing && sourceIsSecondaryEntity) {
        state.preservePrimaryEntityName = false;
        return;
      }

      const preserveName = state.preservePrimaryEntityName;
      const name = preserveName && existing ? existing.name : sourceName;

      state.entities = existing
        ? state.entities.map((entity) => entity.id === primaryEntityId
          ? { ...entity, name, text: action.payload.text, rows }
          : entity)
        : [{ id: primaryEntityId, name, text: action.payload.text, rows }, ...state.entities];
      state.preservePrimaryEntityName = false;
    },
    updateEntity(state, action) {
      const { id, changes } = action.payload;
      state.entities = state.entities.map((entity) => entity.id === id ? { ...entity, ...changes } : entity);
    },
    addEntity(state) {
      const id = `entity_${state.entities.length + 1}`;
      state.entities.push({ id, name: id, text: "id,name\n1,Example", rows: [] });
      state.error = "";
    },
    deleteEntity(state, action) {
      state.entities = state.entities.filter((entity) => entity.id !== action.payload);
      state.previewEntityIds = state.previewEntityIds.filter((id) => id !== action.payload);
      state.collapsedEntityIds = state.collapsedEntityIds.filter((id) => id !== action.payload);
      state.error = "";
    },
    toggleEntityCollapsed(state, action) {
      state.collapsedEntityIds = state.collapsedEntityIds.includes(action.payload)
        ? state.collapsedEntityIds.filter((id) => id !== action.payload)
        : [...state.collapsedEntityIds, action.payload];
    },
    setSearchTerm(state, action) {
      state.searchTerm = action.payload;
    },
    setEntityPreview(state, action) {
      if (!state.previewEntityIds.includes(action.payload)) {
        state.previewEntityIds.push(action.payload);
      }
      state.error = "";
    },
    clearEntityPreview(state, action) {
      state.previewEntityIds = state.previewEntityIds.filter((id) => id !== action.payload);
    },
    setError(state, action) {
      state.error = action.payload;
    },
    preservePrimaryEntityName(state, action) {
      state.preservePrimaryEntityName = action.payload;
    },
  },
});

export const {
  syncPrimaryEntity,
  updateEntity,
  addEntity,
  deleteEntity,
  toggleEntityCollapsed,
  setSearchTerm,
  setEntityPreview,
  clearEntityPreview,
  setError,
  preservePrimaryEntityName,
} = rawDataSlice.actions;

export default rawDataSlice.reducer;
