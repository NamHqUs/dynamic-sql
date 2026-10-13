const backendApiUrl = import.meta.env.VITE_BACKEND_API_URL || "";

export const apiUrls = {
  backend: backendApiUrl,
  dataSource: import.meta.env.VITE_DATA_SOURCE_URL || "",
  database: {
    tables: `${backendApiUrl}/db/tables`,
    tableData: (tableName: string) => `${backendApiUrl}/db/data/${encodeURIComponent(tableName)}`,
  },
  metadata: {
    list: `${backendApiUrl}/metadata`,
    byName: (entityName: string) => `${backendApiUrl}/metadata/${encodeURIComponent(entityName)}`,
    reset: `${backendApiUrl}/metadata/reset`,
  },
  dynamicQuery: {
    execute: `${backendApiUrl}/dynamic-query/query`,
    toString: `${backendApiUrl}/dynamic-query/query/to-string`,
  },
} as const;
