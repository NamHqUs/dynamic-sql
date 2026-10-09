import { configureStore } from "@reduxjs/toolkit";
import dataSourceReducer from "../components/data-sources/data-source-slice";
import rawDataReducer from "../components/data-sources/raw-data/raw-data-slice";

export const store = configureStore({
  reducer: {
    dataSource: dataSourceReducer,
    rawData: rawDataReducer,
  },
});
