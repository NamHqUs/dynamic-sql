import { configureStore } from "@reduxjs/toolkit";
import dataSourceReducer from "../components/data-sources/data-source-slice";
import databaseReducer from "../components/data-sources/database-slice";
import queryBuilderReducer from "../components/query-builder/query-builder/query-builder-slice";

export const store = configureStore({
  reducer: {
    dataSource: dataSourceReducer,
    database: databaseReducer,
    queryBuilder: queryBuilderReducer,
  },
});
