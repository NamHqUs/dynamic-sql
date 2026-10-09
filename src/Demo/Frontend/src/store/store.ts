import { configureStore } from "@reduxjs/toolkit";
import dataSourceReducer from "../components/data-sources/data-source-slice";
import databaseReducer from "../components/data-sources/database-slice";

export const store = configureStore({
  reducer: {
    dataSource: dataSourceReducer,
    database: databaseReducer,
  },
});
